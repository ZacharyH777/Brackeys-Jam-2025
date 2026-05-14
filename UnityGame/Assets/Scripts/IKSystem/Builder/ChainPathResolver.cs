// File: IKSystem/Builders/Path/ChainPathResolver.cs
using UnityEngine;

namespace RunstarSystems.IKSystem.Builders.Path
{
    public static class ChainPathResolver
    {
        private const int path_capacity = 128;

        /* Try build a hierarchy path from start to end, or deepest if end is null.
         * @param start path start
         * @param end optional explicit end
         * @param path resolved path
         * @param error_message failure reason
         */
        public static bool TryBuildPath(
            Transform start,
            Transform end,
            out Transform[] path,
            out string error_message)
        {
            path = null;
            error_message = null;

            if (start == null)
            {
                error_message = "Start transform was missing.";
                return false;
            }

            if (end != null)
            {
                if (!IsChild(end, start))
                {
                    error_message = "End transform was not a child of start transform.";
                    return false;
                }

                path = PathToEnd(start, end, out error_message);
                if (path == null)
                {
                    if (error_message == null)
                    {
                        error_message = "Invalid chain path.";
                    }
                    return false;
                }

                return true;
            }

            path = DeepestPath(start, out error_message);
            if (path == null)
            {
                if (error_message == null)
                {
                    error_message = "Invalid chain path.";
                }
                return false;
            }

            return true;
        }

        private static bool IsChild(Transform child, Transform parent)
        {
            Transform node = child;
            while (node != null)
            {
                if (node == parent)
                {
                    return true;
                }
                node = node.parent;
            }

            return false;
        }

        private static Transform[] PathToEnd(Transform start, Transform end, out string error_message)
        {
            error_message = null;

            Transform[] stack = new Transform[path_capacity];
            int count = 0;

            Transform node = end;
            while (node != null)
            {
                if (count >= path_capacity)
                {
                    error_message = "Path capacity was exceeded.";
                    return null;
                }

                stack[count] = node;
                count++;

                if (node == start)
                {
                    break;
                }

                node = node.parent;
            }

            if (count < 2)
            {
                return null;
            }

            if (stack[count - 1] != start)
            {
                return null;
            }

            Transform[] path = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                path[i] = stack[count - 1 - i];
            }

            return path;
        }

        private static Transform[] DeepestPath(Transform start, out string error_message)
        {
            error_message = null;

            Transform[] best_path = new Transform[path_capacity];
            Transform[] temp_path = new Transform[path_capacity];

            int best_count = 0;
            int temp_count = 0;

            Transform[] node_stack = new Transform[path_capacity];
            int[] child_stack = new int[path_capacity];
            int stack_count = 0;

            node_stack[stack_count] = start;
            child_stack[stack_count] = 0;
            stack_count++;

            while (stack_count > 0)
            {
                int top = stack_count - 1;
                Transform node = node_stack[top];
                int child_index = child_stack[top];

                if (child_index == 0)
                {
                    if (temp_count >= path_capacity)
                    {
                        error_message = "Path capacity was exceeded.";
                        break;
                    }

                    temp_path[temp_count] = node;
                    temp_count++;
                }

                if (child_index < node.childCount)
                {
                    child_stack[top] = child_index + 1;

                    Transform child = node.GetChild(child_index);

                    if (stack_count >= path_capacity)
                    {
                        error_message = "Stack capacity was exceeded.";
                        break;
                    }

                    node_stack[stack_count] = child;
                    child_stack[stack_count] = 0;
                    stack_count++;
                }
                else
                {
                    if (temp_count > best_count)
                    {
                        best_count = temp_count;
                        for (int i = 0; i < best_count; i++)
                        {
                            best_path[i] = temp_path[i];
                        }
                    }

                    stack_count--;

                    if (temp_count > 0)
                    {
                        temp_count--;
                    }
                }
            }

            if (best_count < 2)
            {
                return null;
            }

            Transform[] path = new Transform[best_count];
            for (int i = 0; i < best_count; i++)
            {
                path[i] = best_path[i];
            }

            return path;
        }
    }
}
