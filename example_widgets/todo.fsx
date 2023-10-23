module Todo
#r "nuget: Avalonia"
#r "nuget: Avalonia.FuncUI"
#r "nuget: Material.Icons.Avalonia"
#r "nuget: LiteDB"
#r "../Fomepage/Fomepage.Api/bin/Debug/net7.0/Fomepage.Api.dll"

open System
open System.IO

open Avalonia
open Avalonia.Animation
open Avalonia.Animation.Easings
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Controls.Primitives
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Markup.Xaml.Styling
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Media.Immutable
open Avalonia.Platform
open Avalonia.Styling
open Avalonia.FuncUI.Hosts
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Threading
open Avalonia.FuncUI.Builder

open Avalonia.FuncUI
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Experimental
open Material.Icons
open Material.Icons.Avalonia
open Fomepage.Api

open LiteDB

module DateOnly =
  let today () = DateOnly.FromDateTime (DateTime.Now) 

  let formatString date =
    if date = today () then
      "Today"
    else
      date.ToString("MMMM dd")

module View =
  let setFocus<'a  when 'a :> InputElement> (obj : IView<'a>) =
    obj
    |> View.withOutlet (fun o ->
        o.AttachedToVisualTree.Add(fun _ -> o.Focus() |> ignore))

type InputElement with
  static member onKey<'a when 'a :> InputElement> (key : Key, f : unit -> unit) =
    AttrBuilder.CreateSubscription(InputElement.KeyDownEvent, fun ev -> if ev.Key = key then f ())

[<CLIMutable>]
type TodoItem =
    { ItemId: Guid
      Title: string
      Done: bool
      Date: DateOnly }

[<RequireQualifiedAccess>]
module TodoItem =

    let create (title: string) (date : DateOnly) =
        { TodoItem.ItemId = Guid.NewGuid()
          TodoItem.Title = title
          TodoItem.Done = false
          TodoItem.Date = date }

[<RequireQualifiedAccess>]
module AppState =

    let items: IWritable<TodoItem list> = new State<_>([])

    let activeItemId: IWritable<Guid option> = new State<_>(None)

    let hideDoneItems: IWritable<bool> = new State<_>(false)

    let date : IWritable<DateOnly> = new State<_>(DateOnly.today ())


[<RequireQualifiedAccess>]
module ControlThemes =

    let inlineButton = lazy (
        match Application.Current.TryFindResource "InlineButton" with
        | true, theme -> theme :?> ControlTheme
        | false, _ -> failwithf "Could not find theme 'InlineButton'"
    )

    let doneBrush = lazy (SolidColorBrush(Color.Parse("#b8dc9f"), 0.2) :> IBrush)
    let notDoneBrush = lazy(SolidColorBrush(Color.Parse("#fa6466"), 0.2) :> IBrush)


module Views =

    let listItemView (item: IWritable<TodoItem>) =
        Component.create ($"item-%O{item.Current.ItemId}", fun ctx ->
            let activeItemId = ctx.usePassed AppState.activeItemId
            let item = ctx.usePassed item
            let title = ctx.useState item.Current.Title
            let animation = ctx.useStateLazy(fun () ->
                Animation()
                    .WithDuration(0.3)
                    .WithEasing(CubicEaseIn())
                    .WithFillMode(FillMode.Forward)
                    .WithKeyFrames [
                        KeyFrame()
                            .WithCue(0)
                            .WithSetter(Component.OpacityProperty, 1.0)
                            .WithSetter(TranslateTransform.XProperty, 0.0)

                        KeyFrame()
                            .WithCue(1)
                            .WithSetter(Component.OpacityProperty, 0.0)
                            .WithSetter(TranslateTransform.XProperty, 500.0)
                    ]
            )

            ctx.attrs [
                Component.renderTransform (TranslateTransform())
            ]

            let isActive = Some item.Current.ItemId = activeItemId.Current

            let color = if item.Current.Done then ControlThemes.doneBrush else ControlThemes.notDoneBrush

            if isActive then
                DockPanel.create [
                    DockPanel.children [
                        Button.create [
                            Button.dock Dock.Right
                            Button.margin 5
                            Button.theme ControlThemes.inlineButton.Value
                            Button.content (
                                MaterialIcon.create [
                                  MaterialIcon.width 24
                                  MaterialIcon.height 24
                                  MaterialIcon.kind MaterialIconKind.Floppy
                                  
                                ]
                            )
                            Button.hotKey (KeyGesture Key.Enter)
                            Button.onClick (fun _ ->
                                item.Set { item.Current with Title = title.Current }
                                activeItemId.Set None
                            )
                        ]

                        TextBox.create [
                            TextBox.dock Dock.Left
                            TextBox.text title.Current
                            TextBox.fontSize 14.0
                            TextBox.fontWeight FontWeight.Light
                            TextBox.onTextChanged (fun text -> title.Set text)
                            TextBox.verticalContentAlignment VerticalAlignment.Center
                            TextBox.classes [ "borderless" ]
                            TextBox.focusable true
                            TextBox.onLostFocus (fun _ ->
                              if AppState.activeItemId.Current = Some item.Current.ItemId then
                                AppState.activeItemId.Set None)
                            TextBox.onKey (Key.Escape, fun () -> AppState.activeItemId.Set None)
                        ]
                        |> View.setFocus
                    ]
                ]
            else
                DockPanel.create [
                    DockPanel.background color.Value
                    DockPanel.children [

                        StackPanel.create [
                            StackPanel.dock Dock.Right
                            StackPanel.margin 5
                            StackPanel.spacing 5
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.children [
                                Button.create [
                                    Button.theme ControlThemes.inlineButton.Value
                                    Button.content (
                                        MaterialIcon.create [
                                            MaterialIcon.width 24
                                            MaterialIcon.height 24
                                            MaterialIcon.kind MaterialIconKind.Edit
                                        ]
                                    )
                                    Button.onClick (fun _ -> activeItemId.Set (Some item.Current.ItemId))
                                ]

                                Button.create [
                                    Button.theme ControlThemes.inlineButton.Value
                                    Button.content (
                                        MaterialIcon.create [
                                            MaterialIcon.width 24
                                            MaterialIcon.height 24
                                            MaterialIcon.kind MaterialIconKind.Delete
                                            
                                        ]
                                    )
                                    Button.onClick (fun args ->
                                        if not (ctx.control.IsAnimating Component.OpacityProperty) then
                                            ignore (
                                                task {
                                                    do! animation.Current.RunAsync ctx.control

                                                    Dispatcher.UIThread.Post (fun _ ->
                                                        AppState.items.Current
                                                        |> List.filter (fun i -> i.ItemId <> item.Current.ItemId)
                                                        |> AppState.items.Set
                                                    )

                                                    return ()
                                                }
                                            )
                                    )
                                ]
                            ]
                        ]

                        CheckBox.create [
                            CheckBox.margin (Thickness (10, 0, 0, 0))
                            CheckBox.dock Dock.Left
                            CheckBox.isChecked item.Current.Done
                            CheckBox.horizontalAlignment HorizontalAlignment.Stretch
                            CheckBox.onChecked (fun _ -> item.Set { item.Current with Done = true })
                            CheckBox.onUnchecked (fun _ -> item.Set { item.Current with Done = false })
                            CheckBox.onDoubleTapped (fun _ ->
                              item.Set { item.Current with Done = not item.Current.Done }
                              AppState.activeItemId.Set (Some item.Current.ItemId))
                            CheckBox.content (
                                TextBlock.create [
                                    TextBlock.fontSize 14.0
                                    TextBlock.fontWeight FontWeight.Light
                                    TextBlock.text item.Current.Title
                                ]
                            )
                        ]
                    ]
                ]
        )

    let listView () =
        Component.create ("listView", fun ctx ->
            let allItems = ctx.usePassed AppState.items
            let hideDoneItems = ctx.usePassed AppState.hideDoneItems
            let activeItemId = ctx.usePassed AppState.activeItemId

            StackPanel.create [
                StackPanel.orientation Orientation.Vertical
                StackPanel.verticalScrollBarVisibility ScrollBarVisibility.Visible
                StackPanel.children ([
                    let items =
                        allItems
                        |> State.sequenceBy (fun item -> item.ItemId)
                        |> List.filter (fun item ->
                            if hideDoneItems.Current then
                                not item.Current.Done
                             else
                                 true
                        )

                    for item in items do
                        yield listItemView item

                        yield Rectangle.create [
                            Rectangle.fill Brushes.LightGray
                            Rectangle.height 1.0
                            Rectangle.horizontalAlignment HorizontalAlignment.Stretch
                        ]
                     
                    if activeItemId.Current = None then
                        yield Button.create [
                          Button.theme ControlThemes.inlineButton.Value
                          Button.content (TextBlock.create [
                            TextBlock.textAlignment TextAlignment.Center
                            TextBlock.text "..."
                          ])
                          Button.onClick (fun _ ->
                            let newItem = TodoItem.create "" AppState.date.Current
                            allItems.Set (AppState.items.Current @ [newItem])
                            activeItemId.Set (Some newItem.ItemId))
                        ]

                        yield Rectangle.create [
                              Rectangle.fill Brushes.LightGray
                              Rectangle.height 1.0
                              Rectangle.horizontalAlignment HorizontalAlignment.Stretch
                          ]
                ])
            ]
        )

    let toolbarView () =
        Component(fun ctx ->

            let hideDoneItems = ctx.usePassed AppState.hideDoneItems
            let items = ctx.usePassed AppState.items
            let date = ctx.usePassed AppState.date

            let doneItemsCount =
                items.Current
                |> List.filter (fun item -> item.Done)
                |> List.length

            DockPanel.create [
                DockPanel.background (ImmutableSolidColorBrush(Colors.Black, 0.1))
                DockPanel.children [

                    (* left *)
                    StackPanel.create [
                        StackPanel.dock Dock.Left
                        StackPanel.orientation Orientation.Horizontal
                        StackPanel.spacing 5
                        StackPanel.margin 5
                        StackPanel.children [

                            Button.create [
                                Button.theme ControlThemes.inlineButton.Value
                                Button.content (
                                    StackPanel.create [
                                        StackPanel.horizontalAlignment HorizontalAlignment.Center
                                        StackPanel.orientation Orientation.Horizontal
                                        StackPanel.spacing 5
                                        StackPanel.children [
                                            MaterialIcon.create [
                                                MaterialIcon.width 24
                                                MaterialIcon.height 24
                                                MaterialIcon.kind MaterialIconKind.Add
                                            ]
                                            // TextBlock.create [
                                            //     TextBlock.verticalAlignment VerticalAlignment.Center
                                            //     TextBlock.text "add item"
                                            // ]
                                        ]
                                    ]
                                )
                                Button.onClick (fun _ ->
                                    let newItem = TodoItem.create "" AppState.date.Current
                                    AppState.items.Set (AppState.items.Current @ [newItem])
                                    AppState.activeItemId.Set (Some newItem.ItemId)
                                )
                            ]

                        ]
                    ]

                    (* right *)
                    StackPanel.create [
                        StackPanel.dock Dock.Right
                        StackPanel.orientation Orientation.Horizontal
                        StackPanel.spacing 5
                        StackPanel.margin 5
                        StackPanel.children [
                            Button.create [
                                Button.theme ControlThemes.inlineButton.Value
                                Button.content (
                                    StackPanel.create [
                                        StackPanel.horizontalAlignment HorizontalAlignment.Center
                                        StackPanel.orientation Orientation.Horizontal
                                        StackPanel.spacing 5
                                        StackPanel.children [
                                            MaterialIcon.create [
                                                MaterialIcon.width 24
                                                MaterialIcon.height 24
                                                MaterialIcon.kind (
                                                    if hideDoneItems.Current then
                                                        MaterialIconKind.Show
                                                    else
                                                        MaterialIconKind.Hide
                                                )
                                            ]
                                            // TextBlock.create [
                                            //     TextBlock.verticalAlignment VerticalAlignment.Center
                                            //     TextBlock.text (
                                            //         if hideDoneItems.Current then
                                            //             $"show %i{doneItemsCount} done"
                                            //         else
                                            //             $"hide %i{doneItemsCount} done"
                                            //     )
                                            // ]
                                        ]
                                    ]
                                )
                                Button.onClick (fun _ ->
                                    hideDoneItems.Set (not hideDoneItems.Current)
                                )
                            ]
                        ]
                    ]

                    // View.createGeneric<Panel> [
                    //   Panel.background (SolidColorBrush.Parse "Red")
                    // ]

                    StackPanel.create [
                        StackPanel.margin 0.
                        StackPanel.spacing 5.
                        StackPanel.orientation Orientation.Horizontal
                        StackPanel.horizontalAlignment HorizontalAlignment.Center
                        StackPanel.children [

                            Button.create [
                                Button.dock Dock.Left
                                Button.theme ControlThemes.inlineButton.Value
                                Button.content (
                                    StackPanel.create [
                                        StackPanel.horizontalAlignment HorizontalAlignment.Center
                                        StackPanel.orientation Orientation.Horizontal
                                        StackPanel.spacing 5
                                        StackPanel.children [
                                            MaterialIcon.create [
                                                MaterialIcon.width 24
                                                MaterialIcon.height 24
                                                MaterialIcon.kind MaterialIconKind.ArrowLeft
                                            ]
                                            // TextBlock.create [
                                            //     TextBlock.verticalAlignment VerticalAlignment.Center
                                            //     TextBlock.text "add item"
                                            // ]
                                        ]
                                    ]
                                )
                                Button.onClick (fun _ ->
                                    date.Set (AppState.date.Current.AddDays -1)
                                )
                            ]

                            Button.create [
                              Button.verticalAlignment VerticalAlignment.Center
                              Button.theme ControlThemes.inlineButton.Value
                              Button.onClick (fun _ -> date.Set (DateOnly.today ()))
                              Button.content (TextBlock.create [
                                  TextBlock.textAlignment TextAlignment.Center
                                  TextBlock.fontSize 16.0
                                  TextBlock.text (DateOnly.formatString AppState.date.Current)
                              ])
                            ]
                            

                            Button.create [
                                Button.dock Dock.Right
                                Button.theme ControlThemes.inlineButton.Value
                                Button.content (
                                    StackPanel.create [
                                        StackPanel.horizontalAlignment HorizontalAlignment.Center
                                        StackPanel.orientation Orientation.Horizontal
                                        StackPanel.spacing 5
                                        StackPanel.children [
                                            MaterialIcon.create [
                                                MaterialIcon.width 24
                                                MaterialIcon.height 24
                                                MaterialIcon.kind MaterialIconKind.ArrowRight
                                            ]
                                            // TextBlock.create [
                                            //     TextBlock.verticalAlignment VerticalAlignment.Center
                                            //     TextBlock.text "add item"
                                            // ]
                                        ]
                                    ]
                                )
                                Button.onClick (fun _ ->
                                    date.Set (AppState.date.Current.AddDays 1)
                                )
                            ]
                        ]
                    ]
                ]
            ]

        )

    let mainView () =
        Component.create("todo-mainview", fun ctx ->

            DockPanel.create [
                DockPanel.children [
                    (* toolbar *)
                    ContentControl.create [
                        ContentControl.dock Dock.Top
                        ContentControl.content (toolbarView())
                    ]

                    (* item list *)
                    ScrollViewer.create [
                        ScrollViewer.dock Dock.Top
                        ScrollViewer.content (listView())
                    ]
                ]
            ]
        )

module Resources =

  let resourceDictionary = Path.Combine(__SOURCE_DIRECTORY__, "Resources.axaml")

type TextWidget () =
  interface IWidget with

    override _.Name = "Todo"

    override _.ResourceDictionary = Some Resources.resourceDictionary

    override _.Init db = async {
      let items = db.GetCollection<TodoItem> ()
      AppState.items.Set (List.ofSeq (items.Find(fun x -> x.Date = AppState.date.Current)))
      AppState.items.Observable.Add (fun appItems ->
        items.DeleteMany(fun x -> x.Date = AppState.date.Current) |> ignore
        items.Insert appItems |> ignore
      )
      AppState.date.Observable.Add(fun newDate ->
        AppState.items.Set (List.ofSeq (items.Find(fun x -> x.Date = newDate)))
      )
    }

    override _.View () = Views.mainView ()

    override _.BorderStyle = [
      Border.background (new SolidColorBrush (Colors.Black, 0.4))
      Border.cornerRadius 10.
      Border.padding 0.
      Border.margin 0.
    ]

    override _.Left = 0.1
    override _.Top = 0.1
    override _.Width = 0.3
    override _.Height = 0.5