module Fomepage.Plugin.PluginManager

open System;
open System.IO;
open System.Reflection
open Fomepage.Api
open FsToolkit.ErrorHandling

let loadPluginAssembly (pluginLocation : string) =
    Console.WriteLine($"Loading commands from: {pluginLocation}");
    let loadContext = new PluginLoadContext(pluginLocation);
    loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));

let getWidgets (a : Assembly) =
    try
        let widgets =
            a.GetTypes()
            |> Seq.filter (fun x -> x.IsAssignableTo(typeof<IWidget>))
            |> Seq.map (fun t -> Activator.CreateInstance(t) :?> IWidget)
            |> List.ofSeq
        if List.isEmpty widgets then
            printfn $"Warning: No widget in {a.FullName}"
        Ok widgets
    with
    | e -> Error e.Message

let compilePlugin log (path : string) = asyncResult {
    let outputFolder = Path.Combine(Path.GetDirectoryName(path), "out")
    let fileName = Path.ChangeExtension(Path.GetFileName(path) , ".dll")
    let dllPath = Path.Combine(outputFolder, fileName)
    do! PluginCompiler.compilePlugin log path dllPath
    return dllPath
}

let loadPlugin log path = asyncResult {
    let! dllPath = compilePlugin log path
    return! dllPath |> loadPluginAssembly |> getWidgets
}