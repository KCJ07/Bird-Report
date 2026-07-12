// function to allow this app to run locally

using Amazon.Lambda.TestUtilities;

var function = new Function();
var context = new TestLambdaContext(); // fake ILambdaContext, just for local testing

await function.FunctionHandler(new object(), context);

Console.WriteLine("Done.");