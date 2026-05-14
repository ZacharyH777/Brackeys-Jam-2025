using UnityEngine;
using RunstarSystems.IKSystem.Data;
using RunstarSystems.IKSystem.Builders.Path;
using RunstarSystems.IKSystem.Safety;

namespace RunstarSystems.IKSystem.Builders
{
    public static class IkSubChainBuildPipeline
    {
        public static bool TryBuild(
            string chainName,
            int subChainIndex,
            in IkSubChainBuildSettings settings,
            out IkSubChainData result,
            out Transform[] path,
            out string error_message)
        {
            // Initialize the out parameters
            result = new IkSubChainData { name = chainName };
            path = null; 
            error_message = null;

            Transform start_root = settings.start;
            if (start_root == null)
            {
                error_message = "Start transform was missing.";
                return false;
            }

            // Path is populated here and will bubble up to the IkChainBuilder
            if (!ChainPathResolver.TryBuildPath(start_root, settings.end, out path, out error_message))
            {
                return false;
            }

            if (!IkBuildSafety.ValidatePath(path, out error_message))
            {
                return false;
            }

            if (!ChainLayoutBuilder.TryBuild(in settings, subChainIndex, path, ref result, out error_message))
            {
                return false;
            }

            return true;
        }
    }
}
