module Fomepage.Plugin.PluginCompiler

open System.Reflection
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open FSharp.Compiler.Diagnostics
open FsToolkit.ErrorHandling

open System
open System.IO

open FSharp.Compiler.CodeAnalysis
open Fomepage.Api

let filterImports source =
    let file = Path.ChangeExtension(Path.GetTempFileName(), ".fs")
    File.ReadAllLines source
    |> Seq.filter (fun line -> not (line.StartsWith "#"))
    |> fun lines -> File.WriteAllLines (file, lines)
    file

let compilePlugin log source destination : Async<Result<unit, string>> = asyncResult {
    let checker = lazy FSharpChecker.Create()
    
    if not (File.Exists source) then
        return! Error $"File {source} does not exist"
    
    let libDir = Path.GetDirectoryName(Assembly.GetAssembly(typeof<IWidget>).Location)
    let dlls =
        Directory.GetFiles libDir
        |> Seq.filter (fun x -> Path.GetExtension x = ".dll")
        |> Array.ofSeq
    
    if not (Path.Exists destination) || File.GetLastWriteTime(destination) < File.GetLastWriteTime source then
        log "Compiling plugin"
        let source' = filterImports source        
        
        let! errors1, exitCode1 = 
            checker.Value.Compile(Array.append [| "fsc"; "--targetprofile:netcore"; "-o"; destination; "-a"; source' |] (Array.map (fun x -> $"-r:{x}") dlls))
        
        for error in errors1 do
            match error.Severity with
            | FSharpDiagnosticSeverity.Error -> return! Error error.Message
            | _ -> printfn $"Warning: %s{error.Message}"
    else
        log "Plugin Already Compiled"
}