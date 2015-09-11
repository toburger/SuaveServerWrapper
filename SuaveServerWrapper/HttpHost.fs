﻿namespace SuaveServerWrapper
open Suave
open Suave.Http
open Suave.Http.Applicatives
open Suave.Types
open Suave.Web
open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Net
open System.Net.Http

exception ServerHasBeenAlreadyStopped

type public HttpHost(port: int) =
    let port = Sockets.Port.Parse (port.ToString())
    let glock = Object
    let cancellationTokenSource = new CancellationTokenSource()

    let toSuaveRespnse (m: HttpResponseMessage) =
        async {
            let! content = if m.Content = null then async { return [||] } else (m.Content.ReadAsByteArrayAsync() |> Async.AwaitTask)
            return { status = (HttpCode.TryParse (m.StatusCode |> int)).Value
                     headers = m.Headers
                     |> if m.Content <> null then Seq.append m.Content.Headers else Seq.append Seq.empty<KeyValuePair<string, IEnumerable<string>>>
                     |> Seq.map (fun pair -> pair.Key, pair.Value |> Seq.head)
                     |> Seq.toList
                     content = Bytes content
                     writePreamble = true }
        }

    let toSystemNetRequest (r: HttpRequest) =
        let httpMethod = HttpMethod(r.``method``.ToString())
        let s = new HttpRequestMessage (httpMethod, r.url)
        let isContentHeader =
            let contentHeaders = [|"Content-Disposition"; "Content-Encoding"; "Content-Language"; "Content-Length"
                                   "Content-Location"; "Content-MD5"; "Content-Range"; "Content-Type"|]
            fun (h, _) -> contentHeaders |> Array.exists (fun v -> (v.Equals(h, StringComparison.OrdinalIgnoreCase)))
        r.headers |> List.filter (fun h -> not <| isContentHeader h) |> List.iter (fun (k, v) -> s.Headers.Add (k, v))
        s.Content <- new ByteArrayContent(r.rawForm)
        r.headers |> List.filter isContentHeader |> List.iter (fun (k, v) -> s.Content.Headers.Add (k, v))
        s

    member this.OpenAsync (a: Func<HttpRequestMessage, Task<HttpResponseMessage>>): Task =
        let handleAll (ctx: HttpContext) =
            async {
                let! res = ctx.request |> toSystemNetRequest |> a.Invoke |> Async.AwaitTask
                let! result = res |> toSuaveRespnse
                return Some { ctx with response = result }
            }

        let app =
            choose [
                pathRegex "(.*)" >>= handleAll
            ]

        let config = { defaultConfig with bindings = [ HttpBinding.mk HTTP IPAddress.Loopback port ] }
        lock glock (fun () -> 
            if cancellationTokenSource.IsCancellationRequested then raise <| ServerHasBeenAlreadyStopped
            let listening, server = startWebServerAsync config app
            Async.Start(server, cancellationTokenSource.Token)
            listening |> Async.Ignore |> fun (a: Async<unit>) -> Task.Factory.StartNew(fun () -> a |> Async.RunSynchronously))

    member this.Close () =
        lock glock (fun () ->
            if not cancellationTokenSource.IsCancellationRequested then cancellationTokenSource.Cancel())

    interface IDisposable with
        member this.Dispose() =
            this.Close()
            cancellationTokenSource.Dispose()