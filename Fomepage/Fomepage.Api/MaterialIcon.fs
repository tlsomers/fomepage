namespace Fomepage.Api

open Material.Icons
open Material.Icons.Avalonia

[<AutoOpen>]
module MaterialIcon =
    open System.Collections
    open Avalonia.Controls
    open Avalonia.FuncUI.Types
    open Avalonia.FuncUI.Builder
    open Avalonia.FuncUI.DSL
    
    let create (attrs: IAttr<MaterialIcon> list): IView<MaterialIcon> =
        ViewBuilder.Create<MaterialIcon>(attrs)
     
    type MaterialIcon with

        static member kind<'t when 't :> MaterialIcon>(kind : MaterialIconKind) : IAttr<'t> =
            AttrBuilder<'t>.CreateProperty<MaterialIconKind>(MaterialIcon.KindProperty, kind, ValueNone)