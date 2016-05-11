namespace Catalogue

open Chessie.ErrorHandling
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
open System.Dynamic
open System.IO
open System.Linq
open System.Text
open System.Text.RegularExpressions
open Fake

/// Represents the front matter of a page
type FrontMatter = 
    { /// Title of the page
      Title : string
      /// Unique Id for the post. Can be auto generated from title 
      Id : string
      /// Contains all the variables defined in the top matter
      Vars : JObject
      /// Layout used by the page/post. Can default to the one specified in Settings
      Layout : string
      /// Permanent link of the page. Can be generated from the global pattern
      Permalink : string
      /// Full link to the page
      Link : string
      /// Order of the page in the navigation 
      Order : int
      /// Area associated with the page. Defaults to Home
      Area : Area
      /// Signifies if the file should be excluded from the search index
      ExcludeFromSearchIndex : bool
      /// Signifies if the output should be added to single file
      ExcludeFromSingleFile : bool
      /// Specify if the page content be passed through a markdown processor
      NoMarkdown : bool
      /// Tags specified on the post
      Tags : string []
      /// Relative path of the file from the root directory
      RelativePath : string
      /// Original markdown content of the page
      MarkDown : StringBuilder
      /// Content without front matter
      Content : StringBuilder }
    member this.ToJobject() = 
        this.Vars.["id"] <- new JValue(this.Id)
        this.Vars.["layout"] <- new JValue(this.Layout)
        this.Vars.["title"] <- new JValue(this.Title)
        this.Vars.["permalink"] <- new JValue(this.Permalink)
        this.Vars.["area"] <- new JValue(this.Area.Id)
        this.Vars.["order"] <- new JValue(this.Order)
        this.Vars

module Links = 
    /// Automatically generate a id from title by replacing all the spaces with dashes
    let generateId (title : string) = title.ToLowerInvariant().Replace("  ", " ").Replace("/r", "").Replace(" ", "-")
    
    /// Returns permalink and full link to the page
    /// (permalink, full-link) 
    let generatePermalink (link : string) (fm : FrontMatter) (settings : Settings) = 
        let mutable permalink = link
        if permalink.Contains("{area}") then permalink <- permalink.Replace("{area}", fm.Area.Id)
        if permalink.Contains("{id}") then permalink <- permalink.Replace("{id}", fm.Id)
        /// Permalink has extension specified so don't append anything
        match permalink with
        | Endswith "/index.html" -> (permalink.Replace("/index.html", ""), permalink)
        | Endswith ".html" | Endswith ".htm" -> (permalink, permalink)
        | _ -> (permalink, permalink + "/index.html")
    
    let getTargetFile (permalink : string) (outputFolder : string) = 
        let rec removeTillCharExists (character : string) (input : string) = 
            if input.StartsWith(character) then removeTillCharExists character (input.Substring(1))
            else input
        
        let endPath = 
            if permalink.StartsWith("\\") then removeTillCharExists "\\" permalink
            else if permalink.StartsWith("/") then removeTillCharExists "/" permalink
            else permalink
        
        outputFolder +/ endPath

    /// Create a relative path to a file from a directory
    /// This is useful for mapping input folder files to output folder
    let getRelativePath(filePath: string) (rootDir : string) =
        //(new Uri(rootDir)).MakeRelativeUri(new Uri(filePath)).OriginalString
        filePath.Replace(rootDir, String.Empty)

module TableOfContents = 
    open System.Collections.Generic
    
    /// Add page-name to all the heading. This is to make headings
    /// globablly unique. So that our single concatenated file has
    /// all unique heading 
    let processResults (pageName : string) (result : RenderResult) = 
        let mutable res = result.RenderedMarkDown
        for heading in result.Headings do
            let newAnchor = sprintf "%s/%s" pageName heading.Anchor
            res <- res.Replace(heading.Anchor, newAnchor)
            heading.Anchor <- sprintf "%s/%s" pageName heading.Anchor
        { RenderedMarkDown = res
          Headings = result.Headings }

/// Parses the front matter present at the beginning of the file
/// This is used by both Page and Post
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FrontMatter = 
    let private frontMatterRegex = new Regex(@"(^-{3}[\s\S]*?)-{3}", RegexOptions.Compiled)
    let id = "id"
    let title = "title"
    let permalink = "permalink"
    let layout = "layout"
    let page = "page"
    let area = "area"
    let order = "order"
    let excludeFromSingleFile = "excludefromsinglefile"
    let noMarkdown = "nomarkdown"
    let excludeFromSearchIndex = "excludefromsearchindex"
    
    /// Parse the content for the presence of front matter.
    /// Return the front matter in form of a JObject which can
    /// be used for dynamic lookup.
    let parse (content : string) = 
        let m = frontMatterRegex.Match(content)
        if m.Success then 
            try 
                let fm = m.Groups.[1].Value |> Yaml.parseToJObject
                ok <| (fm, content.Replace(m.Groups.[0].Value, ""))
            with e -> fail <| sprintf "Front matter cannot be parsed. \n%s" (exceptionAndInnersToString e)
        else fail "No front matter found in the document."
    
    let private titleIsRequiredError = "Property 'title' is required in front matter."
    let private permalinkIsRequired = 
        "Property 'permalink' is required in front matter when 'PageDefaultPermalink' property is not defined in the settings.json file."
    let private layoutIsRequired = 
        "Property 'layout' is required in front matter when 'PageDefaultLayout' property is not defined in the settings.json file."
    let private layoutNotFound = 
        sprintf "Specified layout: '%s' is not found. Please copy the file to the '_layouts' folder."
    let private areaNotFound = sprintf "Specified area: '%s' is not found. Please specify in the _settings.json file."
    
    let getIdAndTitle (json : JObject) = 
        match (json.Get id, json.Get title) with
        | Some id, Some title -> ok <| (id, title)
        | _, Some title -> ok <| (Links.generateId <| title, title)
        | _, _ -> fail titleIsRequiredError
    
    let getPermalink (json : JObject) (settings : Settings) (isPage : bool) = 
        match json.Get permalink with
        | Some permalink -> ok <| permalink.ToString()
        | _ -> 
            if isPage then ok settings.PagePermalink
            else fail permalinkIsRequired
    
    let getLayout (json : JObject) (settings : Settings) (isPage : bool) (layouts : Map<string, string>) = 
        match json.Get layout with
        | Some layout -> 
            match layouts.ContainsKey(layout.ToString()) with
            | true -> ok <| layout.ToString()
            | false -> fail <| layoutNotFound (layout.ToString())
        | _ -> 
            if isPage then ok settings.PageLayout
            else fail layoutIsRequired
    
    let getArea (json : JObject) (settings : Settings) = 
        match json.Get area with
        | Some area -> 
            /// Check if the area exists in the settings file
            match settings.Areas |> Array.tryFind (fun cat -> cat.Id = area.ToString()) with
            | Some c -> ok <| c
            | None -> fail <| areaNotFound (area.ToString())
        | _ -> ok Area.HomeArea
    
    let getOrder (json : JObject) = 
        match json.Get order with
        | Some order -> 
            match Int32.TryParse <| order.ToString() with
            | true, o -> o
            | _ -> 0
        | _ -> 0
    
    let getExcludeFromSingleFile (json : JObject) =
        match json.Get excludeFromSingleFile with
        | Some res ->
            match Boolean.TryParse(res) with
            | true, b -> b
            | _ -> false
        | _ -> false
    
    let getNoMarkdown (json : JObject) =
        match json.Get noMarkdown with
        | Some res -> true
        | _ -> false

    let getExcludeFromSearchIndex (json : JObject) =
        match json.Get excludeFromSearchIndex with
        | Some res -> true
        | _ -> false

    /// Generates front matter along with the parsed meta data
    /// This is a common entry point for both Page and Posts
    let getFrontMatter (relativePath : string) (settings : Settings) (layouts : Map<string, string>) (isPage : bool) (content : string) = 
        trial { 
            let! (vars, content) = parse content
            let! (id, title) = getIdAndTitle vars
            let! permalink = getPermalink vars settings isPage
            let! layout = getLayout vars settings isPage layouts
            let! area = getArea vars settings
            let order = getOrder vars 
            let fm = 
                { Vars = vars
                  Order = order
                  ExcludeFromSingleFile = getExcludeFromSingleFile vars
                  ExcludeFromSearchIndex = getExcludeFromSearchIndex vars
                  Id = id
                  RelativePath = relativePath
                  Title = title
                  Permalink = permalink
                  Link = permalink
                  Tags = [||] // TODO: Add tags support for blog posts
                  Layout = layout
                  NoMarkdown = getNoMarkdown vars
                  MarkDown = new StringBuilder(content, 4096)
                  Content = new StringBuilder(content, 4096)
                  Area = area }
            
            let (plink, fullLink) = Links.generatePermalink fm.Permalink fm settings
            return { fm with Permalink = plink
                             Link = fullLink }
        }
