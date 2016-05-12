namespace Catalogue

open Chessie.ErrorHandling
open Fake
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
open System.Collections.Generic
open System.Dynamic
open System.IO
open System.Linq
open System.Text
open System.Text.RegularExpressions

[<CLIMutableAttribute>]
type Context = 
    { Root : RootDirectory
      Settings : Settings
      BuildTasks : BuildTasks
      IsDevMode : bool
      JSEngine : JsEngineWrapper
      HandlebarsContext : JObject
      Data : JObject
      LinkMap : Map<string, FrontMatter>
      Partials : Map<string, string>
      Layouts : Map<string, string>
      Pages : FrontMatter [] }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Context = 
    /// Scans all the pages from the pages folder for meta data and generates initial page objects
    let scanAllPages (rd : RootDirectory) (settings : Settings) (layouts : Map<string, string>) = 
        printHeader "Process pages for front matter"
        loopFilesExt Extension.md (rd.Path +/ SpecialDir.pages)
        |> Seq.map (fun filePath -> 
            let relativePath = Links.getRelativePath filePath rd.Path
            (readFile filePath >>= FrontMatter.getFrontMatter relativePath settings layouts true, filePath))
        |> Seq.filter (fun (result, filePath) -> 
               match result with
               | Ok _ -> true
               | Bad err -> 
                   printWarning "Page:%s. %s" filePath (String.Join(" ", err))
                   false)
        |> Seq.map (fun (result, filePath) -> result |> returnOrFail)
        // The below is necessary to get the previous and next page order right.
        |> Seq.sortBy (fun page -> page.Area.Order, page.Order)
        |> Seq.toArray
    
    let getPagesContext (pages : FrontMatter []) = 
        let jArray = new JArray()
        
        let pages = 
            pages
            |> Array.sortBy (fun page -> page.Order)
            |> Array.iter (fun p -> jArray.Add(p.ToJobject()))
        jArray
    
    let getHandleBarsContext (context : Context) = 
        let jObject = new JObject()
        jObject.Add("pages", getPagesContext context.Pages)
        let areaArray = new JArray()
        context.Pages
        |> Array.groupBy (fun page -> page.Area)
        |> Array.sortBy (fun (area, frontMatter) -> area.Order)
        |> Array.map (fun (area, frontMatter) -> 
               new JObject([ new JProperty("area", area.ToJObject())
                             new JProperty("pages", getPagesContext frontMatter) ]))
        |> Array.iter (areaArray.Add)
        jObject.Add("areas", areaArray)
        JObject.FromObject(context.Settings, Json.serializer).Children() |> Seq.iter (jObject.Add)
        jObject.Add("data", context.Data)
        jObject
    
    /// Generates a map containing list of files along with their content
    let getFileMap (rootDir : RootDirectory) (folderName) (extension) = 
        dirExists (rootDir.Path +/ folderName)
        |> returnOrFail
        |> loopFilesExt extension
        |> Seq.map (fun p -> (Path.GetFileNameWithoutExtension(p).ToLowerInvariant(), File.ReadAllText(p)))
        |> Map.ofSeq
    
    /// Builds a map containing all the permalink and their associated page
    let buildPermalinkMap (pages : FrontMatter []) = 
        let mutable map = new Map<string, FrontMatter>([||])
        
        let validatePermalinkIsUnique (page : FrontMatter) = 
            if map.ContainsKey(page.Permalink) then 
                printError "Page id: %s. There is another page with the same permalink." (map.[page.Permalink].Id)
            else map <- map.Add(page.Permalink, page)

        let validatePageIdIsUnique (page: FrontMatter) =
            if map.ContainsKey(page.Id) then 
                printError "Page id: %s. There is another page with the same id." (map.[page.Id].Id)
            else map <- map.Add(page.Id, page)

        pages |> Array.iter validatePermalinkIsUnique
        pages |> Array.iter validatePageIdIsUnique
        map
    
    /// Builds a mapping between categories and associated pages
    let buildAreaMap (pages : FrontMatter []) = 
        pages
        |> Array.groupBy (fun page -> page.Area)
        |> Map.ofArray
    
    /// Create data store by concatenating all the JSON files from
    /// the data folder
    let createDataStore (rd : RootDirectory) = 
        let root = new JObject()
        loopFilesExt Extension.json (rd.Path +/ SpecialDir.data) 
        |> Seq.iter (fun f -> 
               let json = JObject.Parse(File.ReadAllText(f))
               root.Add(Path.GetFileNameWithoutExtension(f).ToLowerInvariant(), json))
        loopFilesExt Extension.yaml (rd.Path +/ SpecialDir.data) 
        |> Seq.iter 
               (fun f -> 
               try 
                   let json = Yaml.parseToJObject (File.ReadAllText(f))
                   root.Add(Path.GetFileNameWithoutExtension(f).ToLowerInvariant(), json)
               with e -> 
                   printError "Unable to parse the data from the file: %s. Error:%s" f (exceptionAndInnersToString e))
        root
    
    /// Create the global context which is shared across the application
    let createContext (rd : RootDirectory) (settings : Settings) (devMode : bool) (existingContext : Context option) = 
        let partials = getFileMap rd SpecialDir.partials Extension.hbs
        let layouts = getFileMap rd SpecialDir.layouts Extension.hbs
        let pages = scanAllPages rd settings layouts
        let areaLookup = buildAreaMap pages
        
        let temp = 
            { Root = rd
              Settings = settings
              BuildTasks = 
                  if devMode then settings.Development
                  else settings.Production
              IsDevMode = devMode
              JSEngine = 
                  match existingContext with
                  | Some c -> c.JSEngine
                  | _ -> JavaScriptEngine.GetWrapper()
              HandlebarsContext = new JObject()
              Data = createDataStore rd
              Partials = partials
              Layouts = layouts
              LinkMap = buildPermalinkMap pages
              Pages = pages }
        { temp with HandlebarsContext = getHandleBarsContext temp }
