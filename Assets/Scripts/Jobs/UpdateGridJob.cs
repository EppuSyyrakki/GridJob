using Unity.Collections;
using Unity.Jobs;

namespace GridSystem.Jobs
{
    public class UpdateGridJob : IJobParallelFor
    {
        [ReadOnly]
        private NativeArray<Tile> tiles;

        public NativeArray<Tile> grid;

        public void Execute(int index)
        {
            grid[tiles[index].index] = tiles[index];
        }
    }
}