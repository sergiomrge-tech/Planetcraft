using UnityEditor;

namespace Planetcraft.EditorTools
{
    // Liga o "Auto-Start on Editor Load" do MCP for Unity na primeira vez que o projeto abre nesta máquina, para o
    // servidor (http://127.0.0.1:8080/mcp) subir sozinho com o editor, sem clicar em Window > MCP for Unity.
    // Só escreve a preferência se ela ainda não existir: quem desligar depois não é sobrescrito.
    [InitializeOnLoad]
    static class McpAutoStart
    {
        const string Key = "MCPForUnity.AutoStartOnLoad";

        static McpAutoStart()
        {
            if (EditorPrefs.HasKey(Key)) return;
            EditorPrefs.SetBool(Key, true);
            // O handler do pacote lê a preferência no carregamento do domínio: recarrega uma vez para valer já.
            EditorApplication.delayCall += EditorUtility.RequestScriptReload;
        }
    }
}
