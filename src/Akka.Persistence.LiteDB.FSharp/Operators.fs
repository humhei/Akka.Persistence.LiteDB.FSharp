
namespace Akka.Persistence.LiteDB.FSharp

open Akka.Persistence
open Akkling.Persistence
open Akka.Actor

[<AutoOpen>]
module Operators =
    let saveSnapshot newState (ctx: Eventsourced<_>) = 
        
        let saveSnapshot = SaveSnapshot(SnapshotMetadata(ctx.Pid, ctx.LastSequenceNr()), newState)
        let result = 
            let snapshotStore: IActorRef = ctx.SnapshotStore
            (snapshotStore.Ask saveSnapshot).Result
            |> unbox

        assert (result = 0)
