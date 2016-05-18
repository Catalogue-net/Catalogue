namespace Catalogue

open FSharp.Interop.Dynamic
open Microsoft.ClearScript.V8
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Newtonsoft.Json.Serialization
open System.Dynamic
open System.IO

type LowercaseContractResolver() = 
    inherit DefaultContractResolver()
    override __.ResolvePropertyName(propertyName) = propertyName.ToLowerInvariant()

/// Represents the Table of content heading
[<CLIMutableAttribute>]
type TocHeading = 
    { Title : string
      mutable Anchor : string
      HeadingLevel : string }

type RenderResult = 
    { RenderedMarkDown : string
      Headings : TocHeading [] }

[<Interface>]
type IMarkdownParser = 
    abstract RenderInline : string -> string
    abstract Render : pageName:string * content:string -> RenderResult

[<Interface>]
type IHandlebars = 
    abstract Compile : templateName:string * template:string -> bool
    abstract Transform : templateName:string * content:JObject -> string
    abstract TransformExpando : templateName:string * content:ExpandoObject -> string
    abstract RegisterPartial : partialName:string * content:string -> bool
    abstract CompileAndTransform : template:string * content:JObject -> string

[<CLIMutableAttribute>]
type IndexDocuments = 
    { Id : int
      // The advantage of using int based Id instead of
      // HREF is to save space. As the index will have multiple
      // references to the id so keeping it as short as possible 
      // will make substantial difference
      Href : string
      Title : string
      Body : string }

type ILunr = 
    abstract CreateIndex : documents:IndexDocuments [] -> string

type IDiagrammer = 
    abstract CreateDiagram : id:string * content:string -> string

type JsEngineWrapper = 
    { HandleBars : IHandlebars
      MarkdownParser : IMarkdownParser
      Lunr : ILunr }

/// Simple wrapper to provide printing from JavaScript
type Print() =
    static member Error(err) = printError "%s" err
    static member Warning(err) = printWarning "%s" err
    static member Verbose(msg) = printVerbose "%s" msg
     
[<Sealed>]
/// Wraps markdown-it JavaScript parser using Google V8 engine and exposes
/// helper methods to render markdown
type JavaScriptEngine() = 
    let constraints = 
        let cons = V8RuntimeConstraints()
        cons.MaxNewSpaceSize <- 64
        cons.MaxExecutableSize <- 64
        cons

    let engine = new V8ScriptEngine(constraints, V8ScriptEngineFlags.DisableGlobalMembers)
    let jsonSerializerSettings = new JsonSerializerSettings()
    
    do 
        // NOTE: Below two are very import for HandleBars null checking to work properly
        jsonSerializerSettings.NullValueHandling <- NullValueHandling.Ignore
        jsonSerializerSettings.ContractResolver <- new LowercaseContractResolver()
        // The below is extremely import as ClearScript is not a browser and does not expose
        // window object. There is no direct support for common-js so the easiest way is to
        // define a window object so that packages like highlightjs can use it to register 
        // the global object.
        engine.Execute("window = this")
        //engine.AddHostType("Console", typeof<System.Console>)
        engine.AddHostType("Print", typeof<Print>)
        engine.Execute(File.ReadAllText(rootPath +/ "JsLibrary/bundle.js")) |> ignore


    let serialize (content) = JsonConvert.SerializeObject(content, jsonSerializerSettings)
    
    interface IMarkdownParser with
        
        member __.Render(pageName, input) = 
            let result = string <| engine.Script?render (pageName, input)
            let headings = JsonConvert.DeserializeObject<TocHeading []>(string <| engine.Script?getHeadings ())
            { RenderedMarkDown = result
              Headings = headings }
        
        member __.RenderInline(input : string) = string <| engine.Script?md?renderInline (input)
    
    interface IHandlebars with
        member __.Compile(templateName, input) = bool <| engine.Script?compile (templateName, input)
        member __.TransformExpando(templateName, content : ExpandoObject) = 
            engine.Script?transform (templateName, serialize content)
        member __.Transform(templateName, content : JObject) = engine.Script?transform (templateName, serialize content)
        
        member __.RegisterPartial(name, content) = 
            engine.Script?handlebars?registerPartial (name, content)
            true
        
        member __.CompileAndTransform(template, content) = 
            engine.Script?compileAndTransform (template, serialize content)
    
    interface ILunr with
        member __.CreateIndex(docs) = string <| engine.Script?createIndex (serialize docs)
    
    interface IDiagrammer with
        member __.CreateDiagram(id, content) = string <| engine.Script?createDiagram (id, content)
    
    static member GetMarkDownParser() = new JavaScriptEngine() :> IMarkdownParser
    static member GetHandlebars() = new JavaScriptEngine() :> IHandlebars
    static member GetLunr() = new JavaScriptEngine() :> ILunr
    static member GetWrapper() = 
        let js = new JavaScriptEngine()
        { MarkdownParser = js :> IMarkdownParser
          HandleBars = js :> IHandlebars
          Lunr = js :> ILunr }
