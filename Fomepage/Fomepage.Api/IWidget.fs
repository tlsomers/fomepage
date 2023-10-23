namespace Fomepage.Api

open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.Types
open LiteDB

type IWidget =
    abstract member Name : string
    
    abstract member ResourceDictionary : Option<string>
    
    abstract member Init : ILiteDatabase -> Async<unit>
    
    abstract member View : unit -> IView
    
    abstract member BorderStyle : List<IAttr<Border>>
    
    abstract member Left : float
    abstract member Top : float
    abstract member Width : float
    abstract member Height : float
