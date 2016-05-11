namespace Catalogue

open Chessie.ErrorHandling
open Fake
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
open System.ComponentModel
open System.IO
open YamlDotNet.Serialization

[<AutoOpenAttribute>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Json = 
    let serializerSettings = 
        let jsonSerializerSettings = new JsonSerializerSettings()
        jsonSerializerSettings.NullValueHandling <- NullValueHandling.Ignore
        jsonSerializerSettings.ContractResolver <- new LowercaseContractResolver()
        jsonSerializerSettings
    
    let serializer = 
        let s = new JsonSerializer()
        s.ContractResolver <- new LowercaseContractResolver()
        s.NullValueHandling <- NullValueHandling.Ignore
        s
    
    let serialize (input : obj) = JsonConvert.SerializeObject(input, serializerSettings)
    
    let deserialize<'T> (content : string) = 
        try 
            ok <| JsonConvert.DeserializeObject<'T>(content)
        with e -> fail <| exceptionAndInnersToString e
    
    /// Find a scalar property in the JSON object
    let findScalar (property : string) (jObj : JObject) = 
        if jObj.Root.HasValues && not <| isNull jObj.[property] && not <| jObj.[property].HasValues then 
            jObj.[property]
            |> string
            |> Some
        else None
    
    /// Find a complex object in the JSON object   
    let findComplex (property : string) (jObj : JObject) = 
        try 
            if jObj.Root.HasValues then 
                if property.Contains(".") then JObject.FromObject <| jObj.SelectToken(property) |> Some
                else if not <| isNull jObj.[property] && jObj.[property].HasValues then 
                    JObject.FromObject jObj.[property] |> Some
                else None
            else None
        with e -> 
            printfn "%s" (exceptionAndInnersToString e)
            None
    
    type JObject with
        member this.Get(propertyName : string) = this |> findScalar propertyName
        member this.GetComplex(propertyName : string) = this |> findComplex propertyName

module Yaml = 
    let private deserializer = new Deserializer(ignoreUnmatched = true)
    let private serializer = new Serializer(SerializationOptions.JsonCompatible)
    
    /// Parses YAML content and returns a JSON object 
    let parseToJObject (content : string) = 
        let textWriter = new StringWriter()
        let yamlObject = deserializer.Deserialize(new StringReader(content))
        serializer.Serialize(textWriter, yamlObject)
        let res = textWriter.ToString()
        JObject.Parse(res)
    
    let deserialize<'T> (content : string) = 
        try 
            ok <| deserializer.Deserialize<'T>(new StringReader(content))
        with e -> fail <| exceptionAndInnersToString e
    
    let deserializeUsingJson<'T>  (content : string) = 
        try
            ok <| JsonConvert.DeserializeObject<'T>(parseToJObject(content).ToString())
        with e -> fail <| exceptionAndInnersToString e