module Util
    open Messages
    open System
    open System.Security.Cryptography
    open Akka.Actor
    open Akka.FSharp
    open FSharp.Json
    open WebSocketSharp.Server
    open WebSocketSharp  
    
    (*Debugging level: 
      1. trace: prints every operations
      2. tweets: prints tweets and security info
      3. security: only prints operations that are related to authentication and digital signature*)
    let mutable LOGLEVEL: string = "none"
    
    let system = ActorSystem.Create("TwitterClone")
    
    let setLogLevel(level: string) = 
        match level with
        | "trace" | "tweets" | "security" ->
            LOGLEVEL <- level
        | _ ->
            printfn """unknown level. Set to default: "none" """

    let strToBytes (strResponse: string) = 
        let byteResponse =
            strResponse
            |> Text.Encoding.ASCII.GetBytes
        byteResponse

    let Base64StrToBytes (strResponse: string) = 
        let byteResponse =
            strResponse
            |> Convert.FromBase64String
        byteResponse

    let bytesToBase64Str (byteResponse: byte []) = 
        let strResponse =
            byteResponse
            |> Convert.ToBase64String
        strResponse      

    let genChallenge() =
        let rng = RNGCryptoServiceProvider.Create()
        let challenge: byte[] = Array.zeroCreate<byte> 32
        rng.GetBytes challenge
        challenge |> bytesToBase64Str

    let isThere(targets, target): bool = 
        let found = targets |> Seq.exists (fun x -> x = target)
        found

    let checkForHashtagOrMention(tweet: string, symbol: char): bool = 
        isThere(tweet, symbol)
        // tweet |> Seq.exists (fun x -> x = symbol)
    
    let selectActor(actorType: string, id: string): ActorSelection = 
        let actorRef = select ("akka://TwitterClone/user/"+ actorType + id) system
        actorRef

    let getTweetsReponse(req: string, 
                         uid: int, 
                         tweets: (string * string)[], 
                         targetID: int, 
                         targetContent: string,
                         signed: bool): string =
        let data: TweetResponse = {
            ReqType = req; 
            UserID = uid; 
            Tweets = tweets;
            TargetID = targetID;
            TargetContent = targetContent;
            DigitalSigned = signed
        }
        let resString: string = Json.serialize data
        resString

    let getConfirmReponse(req, uid): string =
        let data: ConfirmResponse = {
            ReqType = req;
            UserID = uid
        }
        let resString: string = Json.serialize data
        resString

    let getSharedKeyResponse(uid: int, publicKey: string): string =
        let data: SharedKeyInfo = {
            UserID = uid;
            PublicKey = publicKey;
        }
        let reqString: string = Json.serialize data
        reqString

    let reply(req: string, uid: int, session: WebSocketSessionManager, sid: string) =
        let mutable strResponse: string = ""
        let mutable byteResponse: byte [] = Array.empty
        match req with
        | "Register" | "Tweet" | "Retweet" | "Connect" | "Disconnect" | "Subscribe"->
            strResponse <- getConfirmReponse(req, uid)
            // byteResponse <- strToBytes strResponse
        | _ ->
            printfn "unknown pattern"                 

        session.SendTo(strResponse, sid) 
    
    let verifyHMACSignature (message: string, signature: byte [], key: byte []) =     
        let hmac: HMACSHA256 = new HMACSHA256(key)
        let computedSignature = message |> strToBytes |> hmac.ComputeHash |> bytesToBase64Str
        signature |> bytesToBase64Str |> computedSignature.Equals 

    let sendToWithException (uid:int, message: string, session: WebSocketSessionManager, sid: string) =
        try
            session.SendTo(message, sid)   
        with :? WebSocketSharp.WebSocketException ->
            printfn "client%d has already disconnected!" uid