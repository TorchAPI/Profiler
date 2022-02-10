using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Havok;
using Sandbox;
using Sandbox.Engine.Physics;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Profiler.Utils
{
    internal static class VRageUtils
    {
        public static ulong CurrentGameFrameCount => MySandboxGame.Static.SimulationFrameCounter;

        public static Task MoveToGameLoop(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var taskSrc = new TaskCompletionSource<byte>();
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    taskSrc.SetResult(0);
                }
                catch (Exception e)
                {
                    taskSrc.SetException(e);
                }
            });

            return taskSrc.Task;
        }

        /// <summary>
        /// Get the nearest parent object of given type searching up the hierarchy.
        /// </summary>
        /// <param name="entity">Entity to search up from.</param>
        /// <typeparam name="T">Type of the entity to search for.</typeparam>
        /// <returns>The nearest parent object of given type searched up from given entity if found, otherwise null.</returns>
        public static T GetParentEntityOfType<T>(this IMyEntity entity) where T : class, IMyEntity
        {
            while (entity != null)
            {
                if (entity is T match) return match;
                entity = entity.Parent;
            }

            return null;
        }

        public static IEnumerable<IMyEntity> GetEntities(this HkWorld world)
        {
            var entities = new List<IMyEntity>();
            foreach (var rigidBody in world.RigidBodies)
            {
                var body = rigidBody.GetBody();
                var entity = body.Entity;
                entities.Add(entity);
            }

            return entities;
        }

        public static (double Size, Vector3D Center) GetBound(IEnumerable<Vector3D> positions)
        {
            var minPos = positions.Aggregate(Vector3D.MaxValue, (s, n) => Vector3D.Min(s, n));
            var maxPos = positions.Aggregate(Vector3D.MinValue, (s, n) => Vector3D.Max(s, n));
            var size = Vector3D.Distance(minPos, maxPos);
            var center = (minPos + maxPos) / 2;
            return (size, center);
        }

        public static string MakeGpsString(string name, Vector3D coord)
        {
            return $":GPS:{name}:{coord.X:0}:{coord.Y:0}:{coord.Z:0}:";
        }
    }
}