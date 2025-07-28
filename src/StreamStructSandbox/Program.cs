using System.Text;

namespace StreamStructSandbox;
using StreamStruct;

class Program
{
    static async Task Main(string[] args)
    {


        // Create a processor with your stream
        var stream = new EchoMemoryStream();
        var processor = new StreamFieldProcessor(stream);

        // Define the structure: [fieldName:type]
        var definition = "[id:int][name_length:byte][name:name_length]";

        // Write structured data
        var data = "Alice"u8.ToArray();
        await processor.WriteAsync(definition, [42, (byte)data.Length, data]);

        // Read it back
        var result = await processor.ReadAsync(definition);
        if (result.Success)
        {
            result.TryRead<int>("id", out var id); // 42
            result.TryRead<byte>("name_length", out var nameLength); // 5
            result.TryRead<byte[]>("name", out var nameBytes); // 5
            result.TryReadUtf8("name", out var nameData); // Alice
        }
    }
}