namespace Fomepage.Plugin

open System
open System.Reflection
open System.Runtime.Loader

type PluginLoadContext (pluginPath : string) =
    inherit AssemblyLoadContext ()
    
    let resolver = AssemblyDependencyResolver pluginPath
    
    override self.Load (assemblyName : AssemblyName) =
        let path = resolver.ResolveAssemblyToPath assemblyName
        if path <> null
        then self.LoadFromAssemblyPath path
        else null
    
    override self.LoadUnmanagedDll (unmanagedDllName : string) =
        let libraryPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if libraryPath <> null
        then  self.LoadUnmanagedDllFromPath(libraryPath);
        else IntPtr.Zero

