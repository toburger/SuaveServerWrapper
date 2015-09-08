namespace SuaveServer
open Suave
open Suave.Http
open Suave.Http.Applicatives
open Suave.Types
open Suave.Web
open System
open System.Threading
open System.Net
open System.Net.Http

type public HttpHost(port: int) = 
    let port = Sockets.Port.Parse (port.ToString())

    let response result : WebPart = (fun ctx ->
        {ctx with response = result} |> succeed)

    let toSuaveRespnse (m: HttpResponseMessage) = 
        { status = (HttpCode.TryParse (m.StatusCode |> int)).Value
          headers = m.Headers
            |> Seq.map (fun pair -> pair.Key, pair.Value |> Seq.head)
            |> Seq.toList
          content = if m.Content = null then Bytes[||] else Bytes (m.Content.ReadAsByteArrayAsync() |> Async.AwaitTask |> Async.RunSynchronously)
          writePreamble = true }

    let toSystemNetRequest (r: HttpRequest) =
        let httpMethod = HttpMethod(r.``method``.ToString())
        let s = new HttpRequestMessage (httpMethod, r.url)
        let isContentHeader =
            let contentHeaders = [|"Content-Disposition"; "Content-Encoding"; "Content-Language"; "Content-Length"; "Content-Location"; "Content-MD5"; "Content-Range"; "Content-Type"|]
            fun (h, _) -> contentHeaders |> Array.exists (fun v -> (v.Equals(h, StringComparison.OrdinalIgnoreCase)))
        r.headers |> List.filter (fun h -> not <| isContentHeader h) |> List.iter (fun (k, v) -> s.Headers.Add (k, v))
        s.Content <- new ByteArrayContent(r.rawForm)
        r.headers |> List.filter isContentHeader |> List.iter (fun (k, v) -> s.Content.Headers.Add (k, v))
        s

    member this.Start(a: Func<HttpRequestMessage, HttpResponseMessage>, ctx: CancellationTokenSource) =
        let handleAll = request (fun r ->
            r |> toSystemNetRequest |> a.Invoke |> toSuaveRespnse |> response)

        let app =
            choose [
                pathRegex "(.*)" >>= handleAll
            ]

        let config = { defaultConfig with bindings = [ HttpBinding.mk HTTP IPAddress.Loopback port ] }
        let listening, server = startWebServerAsync config app
        Async.Start(server, ctx.Token)
        listening |> Async.RunSynchronously |> ignore
