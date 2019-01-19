open System

type Binop = ADD | SUB | MUL | DIV | MOD | EQ | GT | LT | GTE | LTE | AND | OR

type Unop = NEG | NOT 

type Expr = 
  | IConst of int 
  | SConst of string
  | BConst of bool
  | Var of string
  | Binop of Binop * Expr * Expr 
  | Unop  of Unop * Expr 
  | Ternary  of Expr * Expr * Expr 
  | ExpTuple of Expr list
  | ConstrObject of CSType * Expr list
  | ConstrArray of CSType * Expr
  | Index of Expr * Expr
  | Property of Expr * string
  | Method of Expr * string * Expr list
  | Lambda of Expr * Expr
  | Call  of string * Expr list
 
and CSType = 
  | INT 
  | BOOL
  | INFER 
  | Param of string 
  | Array of CSType 
  | TTuple of CSType list
  | Composite of CSType * CSType list

type ForLoop = { 
   variable : Expr;
   start : Expr;
   stop : Expr; 
   stride : Expr;
   body : Stmt list;
}

and Conditional = { 
   case : Expr; 
   _if : Stmt list;
   _else : Stmt list option;
}

and Stmt = 
  | Simple of Expr
  | DeclAssign of CSType * Expr * Expr
  | Assign of Expr * Expr
  | Condition of Conditional
  | Loop of ForLoop
  | Parallel of ForLoop
  | Block of Stmt list 
  | Return of Expr

and Function = {
  name : string; 
  typ : CSType;
  args : (CSType * Expr) list
  constraints : (CSType * CSType) list
  body : Stmt list;
}

type Program = Function list

let FmtBinop = function 
  | ADD -> "+"
  | SUB -> "-"
  | MUL -> "*"
  | DIV -> "/"
  | MOD -> "%"
  | AND -> "&&"
  | OR  -> "||"
  | GT  -> ">"
  | LT  -> "<"
  | EQ  -> "=="
  | GTE -> ">="
  | LTE -> "<="
 
let FmtUnop = function
  | NEG -> "-"
  | NOT -> "!"

let rec FmtTup fn args = 
    let tup'd = String.concat "," (List.map fn args) in 
    if (List.length args) > 1  then "(" + tup'd + ")" else tup'd  

let rec FmtArgs args = (String.concat "," (List.map FmtExpr args))

and FmtExpr = function 
  | IConst i -> sprintf "%d" i 
  | SConst s -> s 
  | BConst b -> if b then "true" else "false"
  | Var v -> v
  | Binop (op,s1,s2) -> sprintf "(%s %s %s)" (FmtBinop op) (FmtExpr s1) (FmtExpr s2)
  | Unop (op,s) -> sprintf "(%s %s)" (FmtUnop op) (FmtExpr s)
  | Call (name,args) -> sprintf "%s(%s)" name (FmtArgs args)
  | Method (expr,name,args) -> sprintf "%s.%s(%s)" (FmtExpr expr) name (FmtArgs args)
  | ConstrObject (typ,args) -> sprintf "new %s(%s)" (FmtType typ) (FmtArgs args)
  | ConstrArray (typ,arg) -> sprintf "new %s[%s]" (FmtType typ) (FmtExpr arg)
  | ExpTuple exprs -> FmtTup FmtExpr exprs 
  | Index (obj,idx) -> sprintf "%s[%s]" (FmtExpr obj) (FmtExpr idx)
  | Lambda (arg,exp) -> sprintf "%s => %s" (FmtExpr arg) (FmtExpr exp)
  | Property (obj,str) -> sprintf "%s.%s" (FmtExpr obj) str
  | Ternary (c,i,e) -> sprintf "%s ? %s : %s" (FmtExpr c) (FmtExpr i) (FmtExpr e)

and FmtType = function 
  | INT -> "int"
  | BOOL -> "bool"
  | INFER -> "var"
  | Param t -> t
  | Array t -> FmtType t + "[]"
  | TTuple ts -> FmtTup FmtType ts  
  | Composite (typ,typs) -> sprintf "%s<%s>" (FmtType typ) (String.concat "," (List.map FmtType typs))

let rec FmtStmt indent stm =
  let next = indent + "\t" in
  let fmtblk b = FmtStmt indent (Block b) in
  let formatted = 
    (match stm with
    | Simple e -> (FmtExpr e) + ";"
    | DeclAssign (t,v,e) -> sprintf "%s %s = %s;" (FmtType t) (FmtExpr v) (FmtExpr e)
    | Assign (v,e) -> sprintf "%s = %s;" (FmtExpr v) (FmtExpr e)
    | Return e -> "return " + FmtExpr e + ";"

    | Block (b) ->
       "{\n" + String.concat ("\n" + indent) (List.map (FmtStmt next) b) + "\n" + indent + "}"
    
    | Condition c -> 
      sprintf "if (%s) %s" (FmtExpr c.case) (fmtblk c._if) +
        (match c._else with 
        | None -> ""
        | Some s -> fmtblk s)
      
    | Loop l -> 
      sprintf "for (%s;%s;%s += %s)%s" 
        (FmtStmt "" (DeclAssign (INT,l.variable,l.start))) 
        (FmtExpr (Binop (LT,l.variable,l.stop)))
        (FmtExpr l.variable) (FmtExpr l.stride)
        (fmtblk l.body)

    | Parallel l -> 
      sprintf "Parallel.For(%s,%s,%s => \n %s);"
        (FmtExpr l.start) (FmtExpr l.stop) (FmtExpr l.variable) (fmtblk l.body))

   in
   indent + formatted

let FmtConstraint (param,constr) = 
  sprintf "where %s : %s" (FmtType param) (FmtType constr)

let FmtFunction (func : Function) = 
  let args = String.concat "," (List.map (fun (t,s) -> FmtType t + " " + (FmtExpr s)) func.args) in
  let sign = sprintf "%s %s\n(%s)" (FmtType func.typ) (func.name) args in
  let cons = String.concat "\n\t" (List.map FmtConstraint func.constraints) in 
  let body = FmtStmt "\t" (Block func.body) in
  sign + cons + body

let FmtProgram = 
  List.map (FmtFunction)
 
let range n = 
  List.init n (id)

let maprange n f =  
  List.map f (range n)
 
let get_param_string offset nth =
    let letter n = Char.ConvertFromUtf32 n in
    String.replicate (nth / 13) (letter ((nth % 13) + offset))
    
let in_param_string = get_param_string 13
let out_param_string = get_param_string 0
let in_param n = Param (in_param_string n)
let out_param n = Param (out_param_string n)

let GH_Structure types =
    Composite (Param "GH_Structure", types)

let get_result_types out graft =
    range out |> List.map (out_param) |> List.map (fun t -> if graft then Array t else t)
   
type ImpalaFunctionArgs = {
    zip_types : CSType list;
    zip_tokens : Expr list; 
    rdx_types : CSType list;
    rdx_tokens : Expr list;
    action_type : CSType;
    action_token : Expr;
    error_type : CSType;
    error_token : Expr;
}

// FN ARGS
let getfnargs zip red out graft =
    let convert s offset i =
        let typestr = (in_param_string (i+offset)) in  
        (Param typestr,Var ((s + typestr).ToLower()))
    in
    let (zip_types,zip_tokens) = range zip |> List.map (convert "zip_"  0)    |> List.unzip   
    let (rdx_types,rdx_tokens) = range red |> List.map (convert "redux_" zip) |> List.unzip
    let zip_strs = List.map (fun t -> GH_Structure [t]) (zip_types)
    let rdx_strs = List.map (fun t -> GH_Structure [t]) (rdx_types)
    let rdx_ls = List.map (fun t -> Composite (Param "List", [t])) rdx_types
    let res_typs = get_result_types out graft in 
    let func_t = Composite (Param "Func", zip_types @ rdx_ls @ [TTuple (res_typs)]) 
    let err_t  = Composite (Param "ErrorChecker", zip_types @ rdx_ls)
    { 
      zip_types = zip_strs;
      zip_tokens = zip_tokens;
      rdx_types = rdx_strs;
      rdx_tokens = rdx_tokens;
      action_type = func_t;
      action_token = Var "action";
      error_type = err_t;
      error_token = Var "error";
    }

    

let declareResults n =  
    let result i =
        let var = sprintf "result%d" i in
        let str_typ = GH_Structure [out_param i] in
        (DeclAssign (INFER,Var var,ConstrObject(str_typ,[])),Var var,str_typ)
    in
    range n |> List.map result |> List.unzip3

let callmax name tokens =    
    [DeclAssign(INFER,Var name,Call("Max",List.map (fun token -> Property (token,"Count")) tokens))]

let getpath tokens =    
    [DeclAssign(INFER,Var "paths", Call("GetPathList",tokens))]

let branchzip basetoken ziptoken offset n = 
    let bzips,zips = range n |> List.map (fun i -> let k = in_param_string (i+offset) in Var (basetoken + k),Var (ziptoken + k)) 
                             |> List.unzip in 
    let zipexprs = (List.map (fun z -> 
        Index (Property(z,"Branches"), 
               Method (Var "Math","Min",
                   [Var "i";
                    Binop(SUB, 
                          Property (Property (z,"Branch"), "Count"), 
                          IConst 1)
                   ])
              )
       ) zips) in 
    List.map2 (fun b z -> (DeclAssign(INFER,b,z),b)) bzips zipexprs |> List.unzip

let ensurepaths = 
    List.map (fun t -> Simple(Method(t,"EnsurePath",[Var "targpath"]))) 
   
let appendranges =
    List.mapi (fun i t -> 
        let item = (sprintf ("Item%d") i) in
        Simple(Method(t,"AppendRange",[Property(Var "compute",item);Var "targpath"])))
  
let appendselect sel_token path_token = 
    List.mapi (fun i t ->
      let item = (sprintf ("Item%d") i) in
      Simple(Method(t,"AppendRange",[
        Method(sel_token,"Select",[Lambda(Var "item",Property (Var "item",item))]);
        path_token
      ]))
    )

let allcountszero tokens = 
    let count_tokens = List.map (fun tk -> Property(tk,"Count")) tokens in
    match count_tokens with 
    | [] -> BConst true
    | x :: xs -> List.fold (fun expr tk -> Binop(AND,expr,tk)) x xs

let getbranchitem = 
    List.map2 (fun z b ->
        let z' = match z with
                 | Var s -> Var (s + "x")
                 | _ -> raise (Failure "Non-var token in getbranchitem")
        in
        DeclAssign(INFER,z',
             Index (b, 
               Method (Var "Math","Min",
                   [Var "j";
                    Binop(SUB, 
                          Property (b, "Count"), 
                          IConst 1)
                   ])
              )
        )
    )
       
let getcompute dest tokens res_typs graft = 
    [DeclAssign(INFER,dest,
        Ternary(
           Method(Var "error","Validate",[ExpTuple tokens]),
           Call("action",tokens),
           (if graft then
                ExpTuple (List.map (fun t -> ConstrArray(t,IConst 0)) res_typs)
           else 
                Var "default")
        )
    )]


let generateFunction zip redx out graft =   
    let args = getfnargs zip redx out graft in
    let (declresult_stms,result_toks,result_typs) = declareResults out in 
    let (declbranchzip_stms,branchzip_tokens) = branchzip "branchzip" "zip" 0 zip in
    let (declrdxzip_stms,rdxzip_tokens) = branchzip "rxbranchredux" "redux" zip redx in 
    
    List.concat [
        declresult_stms;
        callmax "maxbranch" (args.zip_tokens @ args.rdx_tokens); 
        getpath result_toks;

        [Loop { 
            variable = Var "i";
            start = IConst 0; stop = Var "maxbranch"; stride = IConst 1;
            body = List.concat [
                [DeclAssign (INFER,Var "path",Call("GetPath",[Var "paths";Var "i"]))];
                declbranchzip_stms;
                callmax "maxlen" branchzip_tokens;
                
                [Loop {
                    variable = Var "j";
                    start = IConst 0; stop = Var "maxlen"; stride = IConst 1;
                    body = DeclAssign(INFER,Var "targpath",
                            Method(Var "path","AppendElement",[Var "j"])) :: 
                            ensurepaths result_toks 
                }]
            ]
        }];

        [Parallel {
            variable = Var "i";
            start = IConst 0; stop = Var "maxbranch"; stride = IConst 1;
            body = List.concat [
                [DeclAssign (INFER,Var "path",Call("GetPath",[Var "paths";Var "i"]))];
                declbranchzip_stms;
                declrdxzip_stms;
                
                [Condition {
                    case = allcountszero (branchzip_tokens @ rdxzip_tokens);  
                    _if = List.concat [
                        callmax "maxlen" branchzip_tokens;
                         
                        (if graft then
                            [Parallel { 
                                variable = Var "j";
                                start = IConst 0; stop = Var "maxlen"; stride = IConst 1;
                                body = List.concat [
                                    getbranchitem args.zip_tokens branchzip_tokens;
                                    getcompute (Var "compute") (args.zip_tokens @ args.rdx_tokens) (result_typs) graft;
                                    [DeclAssign (INFER,Var "path",Call("GetPath",[Var "paths";Var "i"]))];
                                    appendranges result_toks;
                                ];
                            }] 
                        else
                            List.concat [
                                [DeclAssign (INFER,Var "temp",ConstrArray(TTuple(result_typs),Var"maxlen"));
                                 Parallel {
                                    variable = Var "j"; 
                                    start = IConst 0; stop = Var "maxlen"; stride = IConst 1;
                                    body = List.concat [
                                        getbranchitem args.zip_tokens branchzip_tokens;
                                        getcompute (Index(Var "temp",Var "j")) (args.zip_tokens @ args.rdx_tokens) (result_typs) graft;
                                    ]
                                 }]; 
                                appendselect (Var "temp") (Var "path") result_toks;
                            ]
                        )
                    ]; 
                    _else = None;
                }]

            ]
        }];

        [Return (ExpTuple(result_toks))]
    ]
