module Messages

    open WebSocketSharp.Server
    
    type SharedKeyInfo = {
        UserID: int
        PublicKey: string
    }

    type SignedMessageInfo = {
        UserID: int
        Message: string
        DigitalSignature: string
    }

    type RegisterInfo = {
        UserID: int 
        UserName: string
        PublicKey: (string * string) // TODO
    }

    type ConnectInfo = {
        ReqType: string
        UserID: int 
        HashedMessage: string
        DigitalSignature: string
        HashAlgo: string        
    }

    type SubscribeInfo = {
        UserID: int
        TargetID: int
    }

    type QueryInfo = {
        ReqType: string
        UserID: int 
        TargetID: int
        TargetContent: string //UserName or HashTag
    }

    type TweetInfo = {
        UserID: int 
        UserName: string
        Tweet: string
        TargetName: string //Mentioned or Retweet     
        DigitalSigned: bool
    }

    type TweetResponse = {
        ReqType: string
        UserID: int
        Tweets: array<string*string>
        TargetID: int //Mentioned or Follower
        TargetContent: string (*Connection Challenge*) // TODO  
        DigitalSigned: bool
    }

    type ConfirmResponse = {
        ReqType: string
        UserID: int 
        // TODO: Confirm: string
    }

    type ServerMsg = 
        | RegisterAccount of int * string * (string * string) * WebSocketSessionManager * string
        | Connected of int * WebSocketSessionManager * string
        | ShareSecretKey of int * string * WebSocketSessionManager * string
        | VerifyMessage of string * int * string * string * WebSocketSessionManager * string
        | SendChallenge of int * WebSocketSessionManager * string
        | VerifyChallenge of int * string * string * string * WebSocketSessionManager * string
        | Disconnected of int
        | ReceiveTweets of int * string * bool * WebSocketSessionManager * string
        | ReceiveReTweets of int * string * bool * string * WebSocketSessionManager * string
        | SubscribeTo of int * int * WebSocketSessionManager * string
        | QueryHashtag of int * string * WebSocketSessionManager * string
        | QueryMentioned of int * WebSocketSessionManager * string
        | QuerySubscribed of int * string * WebSocketSessionManager * string
        | CheckConnected of int * string * string
        | PrintDebuggingInfo

    type DBMsg = 
        | Init of int * string
        | StoreTweets of int * string
        | StoreReTweets of int * string * string
        | SendTweets of int * WebSocketSessionManager * string
        | AddToFollowing of int
        | AddToFollower of int
        | StoreMentions of string * string
        | Mentioned of int * WebSocketSessionManager * string
        | LookUpSubscribed of int * WebSocketSessionManager * string
        | PrintUserName
        | PrintTweets