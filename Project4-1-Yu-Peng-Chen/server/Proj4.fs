// #time "on"
// #r "nuget: Akka.FSharp"
// #r "nuget: Akka.TestKit"

open System
// open Database
open Server
// open Client
open TwitterClone
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Json
// open Akka.TestKit
open System.Diagnostics

let globalTimer = System.Diagnostics.Stopwatch()

[<EntryPoint>]
let main argv =
    let server = spawn system "server" serverActor
    let remoteHandler = 
        spawn remoteSystem "RemoteHandler" 
        <| fun mailbox ->
            let rec loop() =
                actor {
                    let! message = mailbox.Receive()
                    let info = Json.deserialize<RecordType> message
                    // let info = JsonValue.Parse(message)
                    let req = info.req
                    let uid = info.uid
                    // let tweet = info?content.AsString()
                    let targetId = info.target
                    match req with
                    | "Start" ->
                        server <! Start true
                        // sender <! sprintf "running"
                    | "RegisterAccount" ->
                        let uName = info.content
                        // printfn "[RemoteHandler] uid: %d" uid
                        server <! RegisterAccount (uid, uName)
                        // sender <! sprintf "client%d registration request received" uid
                    | "ReceiveTweets" ->
                        let tweet = info.content
                        // printfn "[RemoteHandler] client%d tweets: %s" uid tweet
                        server <! ReceiveTweets (uid, tweet)
                    | "ReceiveReTweets" -> 
                        let tweets = info.multipleContent
                        let targetName: string = fst tweets.[0]
                        let tweet: string = snd tweets.[0]
                        server <! ReceiveReTweets (uid, tweet, targetName)
                    | "Connect" ->
                        server <! Connected uid
                    | "Disconnect" ->
                        server <! Disconnected uid
                    | "Subscribe" ->
                        server <! SubscribeTo (uid, targetId)
                    | "QueryHashtag" ->
                        let hashtag = info.content
                        server <! QueryHashtag (uid, hashtag)
                    | "QueryMentioned" ->
                        server <! QueryMentioned uid
                    | "QuerySubscribed" ->
                        let targetName = info.content
                        server <! QuerySubscribed (uid, targetName)
                    | "PrintDebuggingInfo" ->
                        server <! PrintDebuggingInfo
                    | _ ->  failwith "Unknown message"
                    
                    return! loop()
                }
            loop()
    System.Console.ReadLine() |> ignore

    0 // return an integer exit code