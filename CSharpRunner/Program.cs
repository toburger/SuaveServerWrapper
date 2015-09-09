using SuaveServerWrapper;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace CSharpRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().Wait();
        }

        static async Task MainAsync()
        {
            using (var server = new HttpHost(port: 8083))
            {
                await server.OpenAsync(async r =>
                {
                    Console.WriteLine(r.RequestUri);
                    Console.WriteLine(r.Content.ReadAsStringAsync().Result);

                    var ss = await Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = System.Net.HttpStatusCode.OK,
                        Content = new StringContent("Processed Request on: " + DateTime.Now)
                    });
                    return ss;
                });

                Console.ReadLine();

                server.Close();
            }
        }
    }
}
