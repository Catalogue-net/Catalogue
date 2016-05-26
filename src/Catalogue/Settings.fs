namespace Catalogue

open Chessie.ErrorHandling
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
open System.Collections.Generic
open System.ComponentModel
open System.IO
open YamlDotNet.Serialization

[<AutoOpen>]
module Constants = 
    let defaultArea = "home"
    
    module SpecialDir = 
        let pages = "_pages"
        let layouts = "_layouts"
        let partials = "_partials"
        let includes = "_includes"
        let data = "_data"
    
    module Extension = 
        let md = "md"
        let hbs = "hbs"
        let json = "json"
        let yaml = "yaml"

/// Represents the root/starting directory of the processing
type RootDirectory = 
    | Dir of DirectoryInfo
    
    static member Create(path : string) = 
        let root = getAbsolutePath path true
        let dirInfo = new DirectoryInfo(root)
        if dirInfo.Exists then ok <| Dir(dirInfo)
        else fail <| sprintf "Passed 'RootDirectory' not found: %s" path
    
    member this.Info = 
        match this with
        | Dir(di) -> di
    
    member this.Path = this.Info.ToString()

[<CLIMutable>]
type PageLinks = 
    { Title : string
      Id : string
      Link : string
      Order : int }

[<CLIMutableAttribute>]
type Area = 
    { Title : string
      Id : string
      mutable Order : int }
    
    member this.ToJObject() = 
        new JObject([ new JProperty("title", this.Title)
                      new JProperty("id", this.Id)
                      new JProperty("order", this.Order) ])
    
    static member HomeArea = 
        { Title = "Home"
          Id = "home"
          Order = 0 }

/// Represents the build tasks that can be performed by the server
type BuildTasks() =  
    member val GenerateSiteMap = false with get, set
    member val EnableSearch = false with get, set
    member val MinifyJS = false with get, set
    member val ConcatJS = false with get, set
    member val MinifyHTML = false with get, set
    member val MinifyCSS = false with get, set
    member val CompileSCSS = false with get, set
    member val Serve = false with get, set
    member val LiveReload = false with get, set
    member val Watch = false with get, set
    member val CleanOutputDir = false with get, set

    /// Whether to generate all the docs as a single page document which can be
    /// used for creating books or searching. 
    member val GenerateSinglePageDoc = false with get, set
    
    /// Terminates the build forcefully at the end. This is useful in case
    /// one needs to start the web server, then perform tasks using post
    /// build script and terminate the server once the job ends.
    /// This is also essential for build servers.
    member val ForceExit = false with get, set

    /// Defines if the post build script if present should be executed or not?
    member val ExecutePostBuildScript = false with get, set

/// Main settings object which represents the )settings.yml object.
/// Note: This could have been easily a record type but there was too
/// much work involved in setting the default field values during
/// de-serialization.
type Settings() = 
    (* Site related settings *)
    member val SiteName = "SiteName" with get, set
    member val SiteUrl = "SiteUrl" with get, set
    (* Page related settings *)
    member val RepositoryName = "RepositoryName" with get, set
    member val RepositoryHost = "RepositoryHost" with get, set
    member val RepositoryUrl = "Repositoryurl" with get, set
    member val RepositoryEditMeLink = "RepositoryEditMeLink" with get, set
    (* Page related settings *)
    member val PageLayout = "page" with get, set
    member val PagePermalink = "{area}/{id}/index.html" with get, set
    (* Build related settings *)
    member val BuildOutput = "../build" with get, set
    member val HtmlFileExtension = "html" with get, set
    member val SiteMapChangeFrequency = "weekly" with get, set
    
    /// Note this is necessary as we don't want Areas to be present in the
    /// Handlebars context
    [<JsonIgnoreAttribute>]
    member val Areas = [| Area.HomeArea |] with get, set
    
    /// Defines all the build configurations possible
    member val BuildConfiguration : Dictionary<string, BuildTasks> = new Dictionary<string, BuildTasks>() with get, set
    member val HttpServerPort = 8080 with get, set
    
    (*SASS related settings*)
    member val MainCssFile = "/assets/css/site.scss" with get, set
    member val MainJsFile = "/assets/js/site.js" with get, set

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Settings = 
    let fileName = "_settings.yaml"
    let private fileReadError = 
        sprintf 
            "Unable to read the '_settings.yaml' file at: %s. Check if the file is accessible and formatted correctly."
    
    /// Validate the given settings object
    let validate (args : CommandLineArgs) (settings : Settings) = 
        let buildFolderPath = getAbsolutePath settings.BuildOutput true
        settings.BuildOutput <- buildFolderPath
        Directory.CreateDirectory buildFolderPath |> ignore
        //Check if the passed build configuration exists
        if not <| settings.BuildConfiguration.ContainsKey(args.Configuration) then
            fail <| sprintf "The passed build configuration: '%s' is not defined in the '_ settings.yaml' file." args.Configuration
        else
            settings.Areas |> Array.iteri (fun index area -> area.Order <- index)
            ok settings
    
    let create (rootDir : RootDirectory) (args: CommandLineArgs) = 
        readFile (rootDir.Path +/ fileName)
        |> appendError (fileReadError rootDir.Path)
        |> Trial.bind (Yaml.deserialize<Settings> >> appendError (fileReadError rootDir.Path))
        |> Trial.bind (validate args)
