namespace Akka.Persistence.LiteDB.FSharp
open LiteDB.FSharp
open LiteDB
open Akka.Persistence.Snapshot
open Akka.Persistence
open Akka.Configuration
open LiteDB.FSharp.Extensions
open System
open Akka.Serialization
open Akka.Util
open Extensions
open System.Threading.Tasks
open Akka.Actor


[<CLIMutable>]
type LiteDBSnapshot = 
    { Id: int
      PersistenceId: string
      SequenceNr: int64 
      Timestamp: DateTime
      Manifest: string 
      Payload: byte []
      SerializerId: int }

type LiteDBSnapshotStore(config: Config) =
    inherit SnapshotStore()
    let mapper = FSharpBsonMapper()
    let dbPath = config.GetString( "connectString", "persistence_snapshot_store.db")

    let akkaSerialization = Akka.Actor.ActorBase.Context.System.Serialization

    let useCollection (f: LiteCollection<'T> -> 'result) = 
        use db = new LiteDatabase(dbPath, mapper)
        let collection = db.GetCollection<'T>()
        f collection


    override __.LoadAsync(persistenceId: string, criteria: SnapshotSelectionCriteria) =
        async {
            let litedbSnapshots = 
                useCollection (fun col ->
                    col.findMany(fun (litedbSnapshot: LiteDBSnapshot) -> 
                        litedbSnapshot.PersistenceId = persistenceId 
                        && litedbSnapshot.Timestamp <= criteria.MaxTimeStamp
                        && litedbSnapshot.SequenceNr <= criteria.MaxSequenceNr
                        && litedbSnapshot.SequenceNr >= criteria.MinSequenceNr )
                )

            if Seq.isEmpty litedbSnapshots 
            then return null
            else
                let litedbSnapshot = Seq.maxBy (fun litedbSnapshot -> litedbSnapshot.PersistenceId) litedbSnapshots
                let metadata = new SnapshotMetadata(persistenceId, litedbSnapshot.SequenceNr)
                let snapshot = akkaSerialization.Deserialize(litedbSnapshot.Payload, litedbSnapshot.SerializerId, litedbSnapshot.Manifest)
                return new SelectedSnapshot(metadata, snapshot)

        } |> Async.StartAsTask

    override x.SaveAsync(metadata: SnapshotMetadata, snapshot: obj) =
        let sender = x.Sender

        let typedTask = 
            async {
                useCollection (fun col ->
                    let snapshotType = snapshot.GetType()
                     
                    let serializer = 
                        akkaSerialization.FindSerializerForType(snapshotType)

                    let manifest = getManifest serializer snapshot

                    let litedbSnapshot =
                        { Id = 0 
                          PersistenceId = metadata.PersistenceId 
                          SerializerId = serializer.Identifier
                          Payload = serializer.ToBinary(snapshot)
                          SequenceNr = metadata.SequenceNr
                          Timestamp = metadata.Timestamp
                          Manifest = manifest }

                    col.Upsert litedbSnapshot 
                    |> ignore

                    sender.Tell(0)
                    
                ) 
            
            } |> Async.StartAsTask

        typedTask :> Task

    override __.DeleteAsync(metadata: SnapshotMetadata) : Task =
        let typedTask =
            async {
                useCollection (fun col ->
                    col.delete(fun (persistent: LiteDBSnapshot) -> 
                        persistent.PersistenceId = metadata.PersistenceId
                        && persistent.SequenceNr = metadata.SequenceNr)
                ) |> ignore
            } 
            |> Async.StartAsTask
        
        typedTask :> Task

    override __.DeleteAsync(persistenceId: string, criteria: SnapshotSelectionCriteria) : Task =
        let typedTask =
            async {
                useCollection (fun col ->
                    col.delete(fun (litedbSnapshot: LiteDBSnapshot) -> 
                        litedbSnapshot.PersistenceId = persistenceId
                        && litedbSnapshot.SequenceNr >= criteria.MinSequenceNr
                        && litedbSnapshot.SequenceNr <= criteria.MaxSequenceNr
                        && litedbSnapshot.Timestamp <= criteria.MaxTimeStamp )
                ) |> ignore
            } 
            |> Async.StartAsTask
        
        typedTask :> Task