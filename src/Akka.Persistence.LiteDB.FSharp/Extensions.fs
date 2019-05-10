namespace Akka.Persistence.LiteDB.FSharp
module internal Extensions =

    open Akka.Serialization
    open Akka.Util

    [<AutoOpen>]
    module Utils =
        let getManifest (serializer: Serializer) (persistent: obj) = 
            match serializer with 
            | :? SerializerWithStringManifest as serializer -> serializer.Manifest(persistent)
            | serialize ->
                if serialize.IncludeManifest then
                    persistent.GetType().TypeQualifiedName()
                else
                ""