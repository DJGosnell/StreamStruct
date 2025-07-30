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
        var definition = "[id:int][name_length:byte][name:name_length]";

        // Write some data first
        var nameBytes = "Alice"u8.ToArray();
        await processor.WriteAsync(definition, [42, (byte)nameBytes.Length, nameBytes]);

        // Verify the stream contains expected values
        var expectedValues = new object[] { 41, (byte)2, nameBytes };
        var (success, errors) = await processor.VerifyAsync(definition, expectedValues);

        if (success)
        {
            Console.WriteLine("Stream verification passed!");
        }
        else
        {
            foreach (var error in errors)
            {
                Console.WriteLine($"Validation error: {error.ErrorMessage}");
                Console.WriteLine($"  Expected: {error.ExpectedValue}");
                Console.WriteLine($"  Actual: {error.ActualValue}");
            }
        }
    }
}