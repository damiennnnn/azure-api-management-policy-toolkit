// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class SetVariableTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", "setting");
            }
            public void Backend(IBackendContext context) {
                context.SetVariable("Backend", "setting");
            }
            public void Outbound(IOutboundContext context) {
                context.SetVariable("Outbound", "setting");
            }
            public void OnError(IOnErrorContext context) {
                context.SetVariable("OnError", "setting");
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="Inbound" value="setting" />
            </inbound>
            <backend>
                <set-variable name="Backend" value="setting" />
            </backend>
            <outbound>
                <set-variable name="Outbound" value="setting" />
            </outbound>
            <on-error>
                <set-variable name="OnError" value="setting" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile set variable policy in sections"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", Exp(context.ExpressionContext));
            }
            
            string Exp(IExpressionContext context)
                => context.RequestId.ToString();
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="Inbound" value="@(context.RequestId.ToString())" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set variable policy with one line expression"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", Exp(context.ExpressionContext));
            }
            
            string Exp(IExpressionContext context) {
                return context.RequestId.ToString();
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="Inbound" value="@{return context.RequestId.ToString();}" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set variable policy with multi line expression"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", CreateString());
            }
            
            [EvaluatedExpression]
            public static string CreateString(){
                StringBuilder sb = new StringBuilder(); 
                
                for (int i =0; i < 10; i++)
                    sb.Append(i);
                
                return sb.ToString();
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="Inbound" value="0123456789" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set variable policy with evaluated expression"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", CreateString("testinput", 1));
            }
            
            [EvaluatedExpression]
            public static string CreateString(string text, int integer){
                return string.Concat(text, integer.ToString());
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="Inbound" value="testinput1" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set variable policy with evaluated expression with parameters"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public dynamic Properties;

            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", Environment.GetEnvironmentVariable("test"));
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="Inbound" value="testinput" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set variable policy with inlined environment variable"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", File.ReadAllText("test.txt"));
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="Inbound" value="testinput" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set variable policy with inlined file contents"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.SetVariable("Inbound", Properties.Get("test"));
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="Inbound" value="testvalue" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile set variable policy with set external value"
    )]
    public void ShouldCompileSetVariablePolicy(string code, string expectedXml)
    {
        // Set up external inline test values
        Environment.SetEnvironmentVariable("test", "testinput");
        File.WriteAllText("test.txt", "testinput");
        CompileProperties.LoadFromJson(JsonSerializer.Serialize(new Dictionary<string, string>
        {
            { "test", "testvalue" }
        }));

        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}