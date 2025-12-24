using UnityEngine;

public interface IStartPointOverride2D
{
    // rawPressWorld: 実際に押したワールド座標
    // trueを返したら、startWorld が開始点として採用される
    bool TryOverrideStartPoint(Vector3 rawPressWorld, out Vector3 startWorld);
}
