namespace Fomepage

open System
open System.IO
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.FuncUI.Builder
open Avalonia.Input
open Avalonia.Media
open Avalonia.Themes.Fluent
open Avalonia.FuncUI.Hosts
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Threading
open Avalonia.Markup.Xaml
open Fomepage.Api
open Fomepage.Plugin
open LiteDB
open Material.Icons
open Material.Icons.Avalonia
open FsToolkit.ErrorHandling

module AppState =
    
    let pluginLogs : IWritable<(string * string) list> = new State<_>([])
    
    let pluginErrors : IWritable<(string * string) list> = new State<_>([])

module Main =

    let view (widgets : List<IWidget>) =
        Component(fun ctx ->            
            let size = ctx.useState (Size(0, 0))
            let offsets = List.init widgets.Length (fun _ -> ctx.useState (Point(0, 0)))
            let sizeOffsets = List.init widgets.Length (fun _ -> ctx.useState (Point(0, 0)))
            
            let dragging = ctx.useState None
            
            Canvas.create [
                Canvas.onPointerMoved (fun e ->
                    match dragging.Current with
                    | Some (i, pos, isScale) ->
                        let newPos = e.GetPosition(null)
                        let diff = newPos - pos
                        if isScale
                        then sizeOffsets[i].Set(sizeOffsets[i].Current + diff)
                        else offsets[i].Set (offsets[i].Current + diff)
                        dragging.Set (Some (i, newPos, isScale))
                    | None -> ())
                Canvas.onPointerReleased (fun _ -> dragging.Set None)
                Canvas.children [
                    for i, widget in List.indexed widgets do
                        let offset = offsets[i]
                        let sizeOffset = sizeOffsets[i]
                        yield  Border.create (widget.BorderStyle @ [
                            Border.child (widget.View ())
                            Border.left (size.Current.Width * widget.Left + offset.Current.X)
                            Border.top (size.Current.Height * widget.Top + offset.Current.Y)
                            Border.width (size.Current.Width * widget.Width + sizeOffset.Current.X)
                            Border.height (size.Current.Height * widget.Height + sizeOffset.Current.Y)
                            Border.onPointerPressed (fun e -> dragging.Set (Some (i, e.GetPosition(null), e.KeyModifiers.HasFlag(KeyModifiers.Shift))))
                        ])
                    yield StackPanel.create [
                        StackPanel.left 0
                        StackPanel.bottom 0
                        StackPanel.horizontalAlignment HorizontalAlignment.Left
                        StackPanel.orientation Orientation.Vertical
                        StackPanel.children [
                            for plugin in AppState.pluginErrors.Current do
                                yield TextBlock.create [
                                    TextBlock.text $"[{fst plugin}] {snd plugin}"
                                ]
                        ]
                    ]
                ]
                AttrBuilder.CreateSubscription(Canvas.SizeChangedEvent, fun s ->
                    printfn "Size %A" (s.NewSize)
                    size.Set s.NewSize)
            ] 
        )

module Startup =
    
    let view () =
        Component (fun ctx ->
            let plugins = ctx.usePassed AppState.pluginLogs
            DockPanel.create [
                DockPanel.children [
                    StackPanel.create [
                        StackPanel.dock Dock.Bottom
                        StackPanel.horizontalAlignment HorizontalAlignment.Left
                        StackPanel.orientation Orientation.Vertical
                        StackPanel.children [
                            for plugin in plugins.Current do
                                yield TextBlock.create [
                                    TextBlock.text $"[{fst plugin}] {snd plugin}"
                                ]
                        ]
                    ]
                    
                    MaterialIcon.create [
                        MaterialIcon.kind MaterialIconKind.Sync
                        MaterialIcon.width 100
                        MaterialIcon.height 100
                        MaterialIcon.verticalAlignment VerticalAlignment.Center
                        MaterialIcon.horizontalAlignment HorizontalAlignment.Center
                    ]
                ]
            ]
        )

type MainWindow() as self =
    inherit HostWindow()
    do
        base.Title <- "Fomepage"
        base.Background <- Brush.Parse("Transparent")
        base.Content <- Startup.view ()
        base.SystemDecorations <- SystemDecorations.None

module App =
    
    let loadResource (app : Application) log path =
        if File.Exists path then
            log "Loading resources"
            let resource = AvaloniaRuntimeXamlLoader.Load(File.ReadAllText(path))
            Dispatcher.UIThread.Invoke(fun _ -> app.Resources.MergedDictionaries.Add (resource :?> ResourceDictionary))
            Ok ()
        else
            Error $"Cannot find {path}"
    
    let initWidget (app : Application) appFolder log (widget : IWidget) = asyncResult {
        do! widget.ResourceDictionary |> Option.map (loadResource app log) |> Option.defaultValue (Ok ())
       
        let dbPath = Path.Combine(appFolder, "db", widget.Name.ToLowerInvariant() + ".db")
        let db = new LiteDatabase(dbPath)
        do! widget.Init(db)
        
        return widget
    }
    
    let loadPlugin' (app : Application) appFolder log plugin = asyncResult {
        log "Loading plugin"
        let! widgets = PluginManager.loadPlugin log plugin
        log $"Found {widgets.Length} widgets"
        
        let! widgets = List.traverseAsyncResultM (initWidget app appFolder log) widgets
        log $"Initialized {widgets.Length} widgets"
        
        return widgets
    }
    
    let loadPlugin (app : Application) appFolder plugin = async {
        let log s = AppState.pluginLogs.Set (AppState.pluginLogs.Current @ [plugin, s])
        let logError s = log s; AppState.pluginErrors.Set (AppState.pluginErrors.Current @ [plugin, s])
        match! loadPlugin' app appFolder log plugin with
        | Ok widgets -> return widgets
        | Error e ->
            logError $"ERROR {e}"
            return []
    }
    
type App(appFolder : string) =
    inherit Application()

    member this.OnWindow (f : HostWindow -> unit) =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            Dispatcher.UIThread.Invoke (fun () -> f (desktopLifetime.MainWindow :?> HostWindow))
        | _ -> ()
    
    override this.Initialize() =
        this.Styles.Add (FluentTheme())
        this.Styles.Add (MaterialIconStyles(null))
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark
        
        let plugins = Directory.GetFiles(appFolder, "*.fsx")
        
        Async.Start(async {
            let! widgets = Async.Parallel (Array.map (App.loadPlugin this appFolder) plugins)
            this.OnWindow (fun h -> h.Content <- Main.view (List.ofSeq (Seq.collect id widgets)))
        })

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <- MainWindow()
        | _ -> ()

module Program =

    [<EntryPoint>]
    let main(args: string[]) =
        let appfolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "fomepage")
        if not (Directory.Exists(appfolder)) then
            Directory.CreateDirectory(appfolder) |> ignore
            Directory.CreateDirectory(Path.Combine(appfolder, "db")) |> ignore
        
        AppBuilder
            .Configure<App>(fun () -> App appfolder)
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
