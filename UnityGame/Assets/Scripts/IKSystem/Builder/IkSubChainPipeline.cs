using UnityEngine;
using IKSystem.Data;
using IKSystem.Builders.Path;
using IKSystem.Safety;

namespace IKSystem.Builders
{
    public static class IkSubChainBuildPipeline
    {
        public static bool TryBuild(
            in IkSubChainBuildSettings settings,
            out IkSubChainBuildResult result,
            out string error_message)
        {
            result = new IkSubChainBuildResult();
            error_message = null;

            Transform start_root = settings.start;
            if (start_root == null)
            {
                error_message = "Start transform was missing.";
                return false;
            }

            Transform[] path;
            if (!ChainPathResolver.TryBuildPath(start_root, settings.end, out path, out error_message))
            {
                return false;
            }

            if (!IkBuildSafety.ValidatePath(path, out error_message))
            {
                return false;
            }

            if (!ChainLayoutBuilder.TryBuild(in settings, path, out result, out error_message))
            {
                return false;
            }

            return true;
        }
    }
}
