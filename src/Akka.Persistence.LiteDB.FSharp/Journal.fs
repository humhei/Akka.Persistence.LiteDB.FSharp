namespace Akka.Persistence.LiteDB.FSharp

open Akka.Persistence.Journal
open System.Threading.Tasks
open LiteDB
open LiteDB.FSharp
open LiteDB.FSharp.Extensions
open Akka.Configuration
open System.Collections.Immutable
open Akka.Persistence
open Akka.Serialization
open System
open System.Collections.Generic
open Akka.Util
open Akka.Actor



[<CLIMutable>]
type LiteDBPersistent = 
    { Id: int
      PersistenceId: string
      SequenceNr: int64 
      Timestamp: int64 
      Manifest: string 
      Payload: byte []
      SerializerId: int }

[<RequireQualifiedAccess>]
module LiteDBPersistent =
    let asPersistent (akkaSerialization: Serialization) (persistent: LiteDBPersistent) =
        let deserialized = akkaSerialization.Deserialize(persistent.Payload, persistent.SerializerId, persistent.Manifest)
        new Persistent(deserialized, persistent.SequenceNr, persistent.PersistenceId, persistent.Manifest, false, ActorRefs.NoSender, null)

type LiteDBJournal(config: Config) =
    inherit AsyncWriteJournal()
    let mapper = FSharpBsonMapper()
    let dbPath = config.GetString( "connection-string", "persistence_journal.db")

    let akkaSerialization = Akka.Actor.ActorBase.Context.System.Serialization

    let useCollection (f: LiteCollection<'T> -> 'result) = 
        use db = new LiteDatabase(dbPath, mapper)
        let collection = db.GetCollection<'T>()
        f collection

    override x.ReplayMessagesAsync(context, persistenceId, fromSequenceNr, toSequenceNr, max, recoveryCallback) =
        let typedTask =
            async {
                let persistents = useCollection (fun col ->
                    col.findMany(fun (persistent: LiteDBPersistent) -> 
                        persistent.PersistenceId = persistenceId 
                        && persistent.SequenceNr >= fromSequenceNr 
                        && persistent.SequenceNr <= toSequenceNr)
                )

                persistents |> Seq.iter (fun persistent ->
                    recoveryCallback.Invoke (LiteDBPersistent.asPersistent akkaSerialization persistent)
                )
            } 
            |> Async.StartAsTask 

        typedTask :> Task

    override x.DeleteMessagesToAsync(persistenceId, toSequenceNr) =
        let typedTask =
            async {
                useCollection (fun col ->
                    col.delete(fun (persistent: LiteDBPersistent) -> 
                        persistent.PersistenceId = persistenceId 
                        && persistent.SequenceNr <= toSequenceNr)
                ) |> ignore
            } 
            |> Async.StartAsTask
        
        typedTask :> Task

    override x.WriteMessagesAsync(messages) = 
        async {
            let persistents: IEnumerable<LiteDBPersistent> = 
                messages
                |> Seq.collect (fun message -> 
                    message.Payload :?> ImmutableList<IPersistentRepresentation>
                )
                |> Seq.map (fun persistent ->
                    let payload = persistent.Payload
                    let payloadType = payload.GetType()

                    let serializer = 
                        akkaSerialization.FindSerializerForType(payloadType)

                    let manifest = 
                        match serializer with 
                        | :? SerializerWithStringManifest as serializer -> serializer.Manifest(payload)
                        | serialize ->
                            if serialize.IncludeManifest then
                                payloadType.TypeQualifiedName()
                            else
                                ""
                
                    { Id = 0
                      PersistenceId = persistent.PersistenceId
                      SequenceNr = persistent.SequenceNr
                      Timestamp = 0L
                      Manifest = manifest
                      Payload = serializer.ToBinary(payload)
                      SerializerId = serializer.Identifier }
                )

            return
                useCollection(fun (col: LiteCollection<LiteDBPersistent>) ->
                    col.Insert(persistents) |> ignore
                    let exes: exn [] = Array.create (Seq.length persistents) null
                    (exes.ToImmutableList() :> IImmutableList<exn>)
                )
        } |> Async.StartAsTask

    override x.ReadHighestSequenceNrAsync(persistenceId, fromSequenceNr) =
        async {
            let result = 
                useCollection (fun col -> 
                    match col.findMany(fun persistent -> 
                        persistent.PersistenceId = persistenceId 
                        && persistent.SequenceNr >= fromSequenceNr) with 
                    | persistents when Seq.length persistents > 0 -> 
                        persistents 
                        |> Seq.maxBy (fun persistent ->
                            persistent.SequenceNr
                        )
                        |> fun persistent -> persistent.SequenceNr

                    | _ -> 0L
                )
            return result
        } |> Async.StartAsTask

