﻿namespace Catalogue

open Chessie.ErrorHandling
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
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
        let root = getAbsolutePath path
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
    member val GenerateSiteMap = true with get, set
    member val EnableSearch = true with get, set
    member val MinifyJS = true with get, set
    member val ConcatJS = true with get, set
    member val MinifyHTML = true with get, set
    member val MinifyCSS = true with get, set
    member val CompileSCSS = true with get, set
    member val Serve = true with get, set
    member val LiveReload = false with get, set
    member val Watch = false with get, set
    
    /// Whether to generate all the docs as a single page document which can be
    /// used for creating books or searching. 
    member val GenerateSinglePageDoc = true with get, set
    
    static member DevSettings() = 
        new BuildTasks(MinifyJS = false, ConcatJS = true, MinifyHTML = false, MinifyCSS = false, LiveReload = true, 
                       Watch = true)

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
    
    member val Development = BuildTasks.DevSettings() with get, set
    member val HttpServerPort = 8080 with get, set
    member val Production = new BuildTasks() with get, set

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
    let validate (settings : Settings) = 
        let buildFolderPath = getAbsolutePath settings.BuildOutput
        settings.BuildOutput <- buildFolderPath
        Directory.CreateDirectory buildFolderPath |> ignore
        let serializer = new YamlDotNet.Serialization.Serializer()
        let writer = new StringWriter()
        serializer.Serialize(writer, settings)
        Console.WriteLine(writer.ToString())
        //emptyDir buildFolderPath
        settings.Areas |> Array.iteri (fun index area -> area.Order <- index)
        ok settings
    
    let create (rootDir : RootDirectory) = 
        readFile (rootDir.Path +/ fileName)
        |> appendError (fileReadError rootDir.Path)
        |> Trial.bind (Yaml.deserialize<Settings> >> appendError (fileReadError rootDir.Path))
        |> Trial.bind (validate)