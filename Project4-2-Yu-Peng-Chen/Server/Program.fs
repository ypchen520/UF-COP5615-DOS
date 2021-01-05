open System
open WebSocketSharp.Server
open WebSocketSharp
open Akka.Actor
open Akka.FSharp
open Messages
open Util
open Server
open FSharp.Json

let serverActor = spawn system "server" serverActor
let serverWS = WebSocketServer("ws://localhost:8080")

type Register () =
    inherit WebSocketBehavior()
    override x.OnMessage message = 
        // x.IgnoreExtensions <- true
        let info = Json.deserialize<RegisterInfo> message.Data
        if info.UserName = "close" then
            printfn "closing session"
            x.Sessions.CloseSession(x.ID)
        else
            serverActor <! RegisterAccount (info.UserID, info.UserName, info.PublicKey, x.Sessions, x.ID)
    // override x.OnClose message = 
    //     try
    //         printfn "onClose!"   
    //         x.Sessions.CloseSession(x.ID)
    //     with :? WebSocketSharp.WebSocketException ->
    //         printfn "client has already disconnected!"

type ReceiveTW () =
    inherit WebSocketBehavior()
    override x.OnMessage message = 
        let info = Json.deserialize<SignedMessageInfo> message.Data 
        serverActor <! VerifyMessage ("ReceiveTW", info.UserID, info.Message, info.DigitalSignature, x.Sessions, x.ID)

type ReceiveReTW () =
    inherit WebSocketBehavior()
    override x.OnMessage message = 
        let info = Json.deserialize<SignedMessageInfo> message.Data 
        serverActor <! VerifyMessage ("ReceiveReTW", info.UserID, info.Message, info.DigitalSignature, x.Sessions, x.ID)

type Subscribe () =
    inherit WebSocketBehavior()
    override x.OnMessage message = 
        let info = Json.deserialize<SubscribeInfo> message.Data 
        serverActor <! SubscribeTo (info.UserID, info.TargetID, x.Sessions, x.ID)

type Query () =
    inherit WebSocketBehavior()
    override x.OnMessage message = 
        let info = Json.deserialize<QueryInfo> message.Data 
        match info.ReqType with
        | "QueryHashtag" ->
            serverActor <! QueryHashtag (info.UserID, info.TargetContent, x.Sessions, x.ID)
        | "QueryMentioned" ->
            serverActor <! QueryMentioned (info.UserID, x.Sessions, x.ID)
        | "QuerySubscribed" ->
            serverActor <! QuerySubscribed (info.UserID, info.TargetContent, x.Sessions, x.ID)
        | _ -> 
            printfn "[Query] unknown pattern"

type Connect () =
    inherit WebSocketBehavior()
    override x.OnMessage message = 
        // x.IgnoreExtensions <- true
        let info = Json.deserialize<ConnectInfo> message.Data 
        let reqType = info.ReqType
        match reqType with
        | "ReplyChallenge" ->
            serverActor <! VerifyChallenge (info.UserID, info.HashedMessage, info.DigitalSignature, info.HashAlgo, x.Sessions, x.ID)
        | "Connect" -> 
            serverActor <! SendChallenge (info.UserID, x.Sessions, x.ID)
        | "FirstConnect" ->
            serverActor <! Connected (info.UserID, x.Sessions, x.ID)
        | "Disconnect" ->
            serverActor <! Disconnected info.UserID
        | _ -> printfn "[Connect] unknown pattern" 

type ShareKey () =
    inherit WebSocketBehavior()
    override x.OnMessage message = 
        x.IgnoreExtensions <- true
        let info = Json.deserialize<SharedKeyInfo> message.Data 
        serverActor <! ShareSecretKey (info.UserID, info.PublicKey, x.Sessions, x.ID)

let printNothing (data: LogData) (msg: string) =
    // Do nothing
    ()

[<EntryPoint>]
let main argv =
    try
        let logLevel: string = argv.[0] |> string
        if logLevel.Length <> 0 then
            setLogLevel(logLevel) |> ignore
        serverWS.AddWebSocketService<Register> ("/register")
        serverWS.AddWebSocketService<ReceiveTW> ("/receivetw")
        serverWS.AddWebSocketService<ReceiveReTW> ("/receiveretw")
        serverWS.AddWebSocketService<Subscribe> ("/subscribe")
        serverWS.AddWebSocketService<Query> ("/query")
        serverWS.AddWebSocketService<Connect> ("/connect")
        serverWS.AddWebSocketService<ShareKey> ("/sharekey") (*shared secret*)
        // serverWS.Log.Level <- LogLevel.Fatal
        let actionTask = Action<LogData,string>(printNothing)
        serverWS.Log.Output <- actionTask 
        serverWS.Start()
        printfn "Server is running ..."
        printfn "Press any key to terminate"
        Console.ReadLine() |> ignore
    with :? IndexOutOfRangeException ->
        printfn "Wrong number of input arguments! Please refer to the following format:"
        printfn "dotnet run LOG_LEVEL"
    0 // return an integer exit code