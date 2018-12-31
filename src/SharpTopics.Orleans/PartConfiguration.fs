module SharpTopics.Orleans.PartConfiguration

open Microsoft.Extensions.Configuration
open Orleans.ApplicationParts
open System.Collections.Generic
open Orleans.Hosting
open System.Reflection
open SharpFunky
open System

type ApplicationPartConfiguration() =
    member val Parts = List<AppPartConfig>() with get, set

and AppPartConfig() =
    member val Items = List<AppPartItemConfig>() with get, set
    member val WithCodeGeneration = true with get, set

    member this.configParts (parts: IApplicationPartManager) =
        let parts =
            (parts, this.Items)
            ||> Seq.fold (fun parts item ->
                item.configParts parts
            )

        let parts =
            if this.WithCodeGeneration then
                match parts with
                | :? IApplicationPartManagerWithAssemblies as parts ->
                    parts.WithCodeGeneration() :> IApplicationPartManager
                | _ -> parts
                    
            else parts

        parts

and AppPartItemConfig() =
    member val AssemblyPath = "" with get, set
    member val AssemblyFrom = "" with get, set
    member val AssemblyHash = "" with get, set
    member val AssemblyHashAlgorithm = "" with get, set

    member this.configParts (parts: IApplicationPartManager) =
        let part =
            if this.AssemblyPath <> "" then
                let assembly = Assembly.LoadFile(this.AssemblyPath)
                Some <| AssemblyPart(assembly)
            elif this.AssemblyFrom <> "" then
                let assembly =
                    if this.AssemblyHashAlgorithm <> "" && this.AssemblyHash <> "" then
                        let algorithm =
                            Enum.Parse(
                                typeof<System.Configuration.Assemblies.AssemblyHashAlgorithm>,
                                this.AssemblyHashAlgorithm)
                            :?> System.Configuration.Assemblies.AssemblyHashAlgorithm
                        let hash = this.AssemblyHash |> String.fromBase64
                        Assembly.LoadFrom(this.AssemblyFrom, hash, algorithm)
                    else
                        Assembly.LoadFrom(this.AssemblyFrom)
                Some <| AssemblyPart(assembly)
            else None
        match part with
        | Some part -> parts.AddApplicationPart part
        | None -> invalidOp "Application part is incomplete"


let readConfiguration configPath (configuration: #IConfiguration) =
    configuration.GetSection(configPath).Get<ApplicationPartConfiguration>()

let configurePartsFrom (staticConfig: ApplicationPartConfiguration) parts =
    let parts =
        (parts, staticConfig.Parts)
        ||> Seq.fold (fun parts part ->
            part.configParts parts
        )

    ()

let configureParts configPath configuration parts =
    let staticConfig = readConfiguration configPath configuration
    configurePartsFrom staticConfig parts
