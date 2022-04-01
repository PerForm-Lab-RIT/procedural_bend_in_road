using System.Collections.Generic;
using PathCreation.Utility;
using UnityEngine;



/// A vertex path is a collection of points (vertices) that lie along a bezier path.
/// This allows one to do things like move at a constant speed along the path,
/// which is not possible with a bezier path directly due to how they're constructed mathematically.

/// This class also provides methods for getting the position along the path at a certain distance or time
/// (where time = 0 is the start of the path, and time = 1 is the end of the path).
/// Other info about the path (tangents, normals, rotation) can also be retrieved in this manner.

namespace PathCreation
{
    public class PerformVertexPath : MonoBehaviour
    {
        #region Fields


        //enum PathSpace { xyz, xy, xz };

        [SerializeField, HideInInspector]
        public PathSpace space;
        [SerializeField, HideInInspector]
        public Vector3[] localPoints;
        [SerializeField, HideInInspector]
        public Vector3[] localTangents;
        [SerializeField, HideInInspector]
        public Vector3[] localNormals;



        /// Percentage along the path at each vertex (0 being start of path, and 1 being the end)
        [SerializeField, HideInInspector]
        public float[] times;

        /// Total distance between the vertices of the polyline
        [SerializeField, HideInInspector]
        public float length;

        /// Total distance from the first vertex up to each vertex in the polyline
        [SerializeField, HideInInspector]
        public float[] cumulativeLengthAtEachVertex;

        /// Bounding box of the path
        [SerializeField, HideInInspector]
        public Bounds bounds;

        /// Equal to (0,0,-1) for 2D paths, and (0,1,0) for XZ paths
        [SerializeField, HideInInspector]
        public Vector3 up;

        private bool isLeftTurn = false;

        // Default values and constants:
        // const int accuracy = 10; // A scalar for how many times bezier path is divided when determining vertex positions
        // const float minVertexSpacing = .01f;

        // Added by GD
        private float straightLegLength; // length of the straight entrance / entrance portion of the road
        private float circleRadiusM; // circle radius in meters

        [SerializeField, HideInInspector]
        public bool isClosedLoop = false;



        #endregion

        #region Constructors

        ///// <summary> Splits bezier path into array of vertices along the path.</summary>
        /////<param name="maxAngleError">How much can the angle of the path change before a vertex is added. This allows fewer vertices to be generated in straighter sections.</param>
        /////<param name="minVertexDst">Vertices won't be added closer together than this distance, regardless of angle error.</param>
        //public VertexPath(BezierPath bezierPath, Transform transform, float maxAngleError = 0.3f, float minVertexDst = 0) :
        //    this(bezierPath, VertexPathUtility.SplitBezierPathByAngleError(bezierPath, maxAngleError, minVertexDst, VertexPath.accuracy), transform)
        //{ }

        ///// <summary> Splits bezier path into array of vertices along the path.</summary>
        /////<param name="maxAngleError">How much can the angle of the path change before a vertex is added. This allows fewer vertices to be generated in straighter sections.</param>
        /////<param name="minVertexDst">Vertices won't be added closer together than this distance, regardless of angle error.</param>
        /////<param name="accuracy">Higher value means the change in angle is checked more frequently.</param>
        //public VertexPath(BezierPath bezierPath, Transform transform, float vertexSpacing) :
        //    this(bezierPath, VertexPathUtility.SplitBezierPathEvenly(bezierPath, Mathf.Max(vertexSpacing, minVertexSpacing), VertexPath.accuracy), transform)
        //{ }


        /// Internal contructor
        /// 
        public void updatePoints(float straightLegLength, float arcLengthM, float circleRadiusM, bool isLeftTurn)
        {
            // how much of an angle you want to turn  in degrees
            float turnAngle = arcLengthM / circleRadiusM;
            if (turnAngle > Mathf.PI)
            {
                Debug.LogError("Arc Length divided by circle radius must be less than 180 degrees.");

            }
            float intervals = (turnAngle * Mathf.Rad2Deg) / (1.0f); // calculates number of vertices on the arc, spaced out by 1 degree
            int verticesPerMeter = Mathf.RoundToInt(intervals / arcLengthM); // we should calculate the spacing of the vertices, there should be more for longer arcLength (var interval increases with turnAngle)

            length = 2 * straightLegLength + arcLengthM;
            int numVertsOnAStraightLeg = Mathf.RoundToInt(straightLegLength * verticesPerMeter);
            int numVertsOnArc = Mathf.RoundToInt(arcLengthM * verticesPerMeter);
            int numVerts = 2 * numVertsOnAStraightLeg + numVertsOnArc;

            space = PathSpace.xz;

            //isClosedLoop = bezierPath.IsClosed;

            //float arcAngleRads = arcLengthM / Mathf.PI; //unnecessary if passed into the function -AG

            localPoints = new Vector3[numVerts];
            localNormals = new Vector3[numVerts];
            localTangents = new Vector3[numVerts];
            cumulativeLengthAtEachVertex = new float[numVerts];

            /// Percentage along the path at each vertex (0 being start of path, and 1 being the end)
            times = new float[numVerts];

            // bounds = new Bounds((pathSplitData.minMax.Min + pathSplitData.minMax.Max) / 2, pathSplitData.minMax.Max - pathSplitData.minMax.Min);

            // Figure out up direction for path
            //up = (bounds.size.z > bounds.size.y) ? Vector3.up : -Vector3.forward;

            ////////  ////////  ////////  ////////  ////////  ////////  ////////  ////////  ////////  
            ////////  Here is a three vector straight road (a test case!)
            //length = straightLegLength;

            //localPoints[0] = new Vector3(0, 0, -straightLegLength);
            //localTangents[0] = new Vector3(0, 0, 1); // directions, or point in local space?
            //localNormals[0] = new Vector3(1, 0, 0); // directions, or point in local space?
            //cumulativeLengthAtEachVertex[0] = 0;
            //times[0] = cumulativeLengthAtEachVertex[0] / length;

            //localPoints[1] = new Vector3(0, 0, 0);
            //localTangents[1] = new Vector3(0, 0, 1); // directions, or point in local space?
            //localNormals[1] = new Vector3(1, 0, 0); // directions, or point in local space?
            //cumulativeLengthAtEachVertex[1] = straightLegLength/2.0f;
            //times[0] = cumulativeLengthAtEachVertex[1] / length;

            ////////
            ////////  ////////  ////////  ////////  ////////  ////////  ////////  ////////  ////////  
            // The incoming straight leg of the road.

            Vector3 straightPathDir = new Vector3(0, 0, 1); // assuming coordinate system origin is "reset" before each straight path
            Vector3 startingPoint = new Vector3(0, 0, -straightLegLength);

            for (int i = 0; i < numVertsOnAStraightLeg; i++)
            {
                cumulativeLengthAtEachVertex[i] = i * (straightLegLength / numVertsOnAStraightLeg);
                float distanceOnLeg = straightLegLength * ((float)i / numVertsOnAStraightLeg);

                localPoints[i] = startingPoint + straightPathDir * distanceOnLeg;
                localTangents[i] = new Vector3(0, 0, 1);
                localNormals[i] = new Vector3(1, 0, 0);

                times[i] = cumulativeLengthAtEachVertex[i] / length; // tells you how far along the ENTIRE path you are (like a percentage of both straight segments and curve combined)
            }

            // The arc

            float rateOfChangeRads = turnAngle / numVertsOnArc;

            for (int i = numVertsOnAStraightLeg; i < numVertsOnAStraightLeg + numVertsOnArc; i++)
            {
                // move clockwise from pi by rateOfChangeRads * i
                float rad = Mathf.PI - rateOfChangeRads * (float)(i - numVertsOnAStraightLeg);

                // circle center position along the local x axis
                Vector3 circleCenter = new Vector3(circleRadiusM, 0, 0);

                // x,z location relative to the center of the circle
                Vector3 unshiftedPointOnCircle = circleRadiusM * new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad));

                localPoints[i] = unshiftedPointOnCircle + circleCenter;
                localNormals[i] = (circleCenter - localPoints[i]).normalized; //P2 - P1 gives direction, the normal is always the direction vector pointing towards the circle center -AG
                localTangents[i] = Vector3.Cross(localNormals[i], Vector3.up); // tangent will be the result of the cross product between Vector up (left hand rule) and normal. Already normalized. -AG

                cumulativeLengthAtEachVertex[i] = straightLegLength + ((float)(i-numVertsOnAStraightLeg) * arcLengthM / numVertsOnArc);
                times[i] = cumulativeLengthAtEachVertex[i] / length;

            }

            Vector3 newStraightDir = localTangents[numVertsOnAStraightLeg + numVertsOnArc - 1];
            Vector3 newLocalTangent = newStraightDir;
            Vector3 newLocalNormal = localNormals[numVertsOnAStraightLeg + numVertsOnArc - 1];
            Vector3 newStartingPoint = localPoints[numVertsOnAStraightLeg + numVertsOnArc - 1];

            float j = 0;
            for (int i = numVertsOnArc + numVertsOnAStraightLeg; i < numVerts; i++)
            {
                cumulativeLengthAtEachVertex[i] = straightLegLength + arcLengthM + j * (straightLegLength / numVertsOnAStraightLeg); // don't think this is right anymore since we changed i
                float distanceOnLeg = straightLegLength * j / (float)numVertsOnAStraightLeg;
                //((float)(i - (numVertsOnArc + numVertsOnAStraightLeg)) / (float)numVertsOnAStraightLeg);

                localPoints[i] = newStartingPoint + newStraightDir * distanceOnLeg; // straight path direction is now in the direction of the last tangents on curve
                localTangents[i] = newLocalTangent;
                localNormals[i] = newLocalNormal;
                j++;

                times[i] = cumulativeLengthAtEachVertex[i] / length; // not sure what the point of this is? -AG
            }



            // Todo:  add final point ( the end of the exit leg along the final tangent )
        }





        //VertexPath(BezierPath bezierPath, VertexPathUtility.PathSplitData pathSplitData, Transform transform)
        //{
        //    this.transform = transform;
        //    space = bezierPath.Space;
        //    isClosedLoop = bezierPath.IsClosed;
        //    int numVerts = pathSplitData.vertices.Count;
        //    length = pathSplitData.cumulativeLength[numVerts - 1];

        //    localPoints = new Vector3[numVerts];
        //    localNormals = new Vector3[numVerts];
        //    localTangents = new Vector3[numVerts];
        //    cumulativeLengthAtEachVertex = new float[numVerts];
        //    times = new float[numVerts];
        //    bounds = new Bounds((pathSplitData.minMax.Min + pathSplitData.minMax.Max) / 2, pathSplitData.minMax.Max - pathSplitData.minMax.Min);

        //    // Figure out up direction for path
        //    up = (bounds.size.z > bounds.size.y) ? Vector3.up : -Vector3.forward;
        //    Vector3 lastRotationAxis = up;

        //    // Loop through the data and assign to arrays.
        //    for (int i = 0; i < localPoints.Length; i++)
        //    {
        //        localPoints[i] = pathSplitData.vertices[i];
        //        localTangents[i] = pathSplitData.tangents[i];
        //        cumulativeLengthAtEachVertex[i] = pathSplitData.cumulativeLength[i];
        //        times[i] = cumulativeLengthAtEachVertex[i] / length;

        //        // Calculate normals
        //        if (space == PathSpace.xyz)
        //        {
        //            if (i == 0)
        //            {
        //                localNormals[0] = Vector3.Cross(lastRotationAxis, pathSplitData.tangents[0]).normalized;
        //            }
        //            else
        //            {
        //                // First reflection
        //                Vector3 offset = (localPoints[i] - localPoints[i - 1]);
        //                float sqrDst = offset.sqrMagnitude;
        //                Vector3 r = lastRotationAxis - offset * 2 / sqrDst * Vector3.Dot(offset, lastRotationAxis);
        //                Vector3 t = localTangents[i - 1] - offset * 2 / sqrDst * Vector3.Dot(offset, localTangents[i - 1]);

        //                // Second reflection
        //                Vector3 v2 = localTangents[i] - t;
        //                float c2 = Vector3.Dot(v2, v2);

        //                Vector3 finalRot = r - v2 * 2 / c2 * Vector3.Dot(v2, r);
        //                Vector3 n = Vector3.Cross(finalRot, localTangents[i]).normalized;
        //                localNormals[i] = n;
        //                lastRotationAxis = finalRot;
        //            }
        //        }
        //        else
        //        {
        //            localNormals[i] = Vector3.Cross(localTangents[i], up) * ((bezierPath.FlipNormals) ? 1 : -1);
        //        }
        //    }

        //    // Apply correction for 3d normals along a closed path
        //    if (space == PathSpace.xyz && isClosedLoop)
        //    {
        //        // Get angle between first and last normal (if zero, they're already lined up, otherwise we need to correct)
        //        float normalsAngleErrorAcrossJoin = Vector3.SignedAngle(localNormals[localNormals.Length - 1], localNormals[0], localTangents[0]);
        //        // Gradually rotate the normals along the path to ensure start and end normals line up correctly
        //        if (Mathf.Abs(normalsAngleErrorAcrossJoin) > 0.1f) // don't bother correcting if very nearly correct
        //        {
        //            for (int i = 1; i < localNormals.Length; i++)
        //            {
        //                float t = (i / (localNormals.Length - 1f));
        //                float angle = normalsAngleErrorAcrossJoin * t;
        //                Quaternion rot = Quaternion.AngleAxis(angle, localTangents[i]);
        //                localNormals[i] = rot * localNormals[i] * ((bezierPath.FlipNormals) ? -1 : 1);
        //            }
        //        }
        //    }

        //    // Rotate normals to match up with user-defined anchor angles
        //    if (space == PathSpace.xyz)
        //    {
        //        for (int anchorIndex = 0; anchorIndex < pathSplitData.anchorVertexMap.Count - 1; anchorIndex++)
        //        {
        //            int nextAnchorIndex = (isClosedLoop) ? (anchorIndex + 1) % bezierPath.NumSegments : anchorIndex + 1;

        //            float startAngle = bezierPath.GetAnchorNormalAngle(anchorIndex) + bezierPath.GlobalNormalsAngle;
        //            float endAngle = bezierPath.GetAnchorNormalAngle(nextAnchorIndex) + bezierPath.GlobalNormalsAngle;
        //            float deltaAngle = Mathf.DeltaAngle(startAngle, endAngle);

        //            int startVertIndex = pathSplitData.anchorVertexMap[anchorIndex];
        //            int endVertIndex = pathSplitData.anchorVertexMap[anchorIndex + 1];

        //            int num = endVertIndex - startVertIndex;
        //            if (anchorIndex == pathSplitData.anchorVertexMap.Count - 2)
        //            {
        //                num += 1;
        //            }
        //            for (int i = 0; i < num; i++)
        //            {
        //                int vertIndex = startVertIndex + i;
        //                float t = num == 1 ? 1f : i / (num - 1f);
        //                float angle = startAngle + deltaAngle * t;
        //                Quaternion rot = Quaternion.AngleAxis(angle, localTangents[vertIndex]);
        //                localNormals[vertIndex] = (rot * localNormals[vertIndex]) * ((bezierPath.FlipNormals) ? -1 : 1);
        //            }
        //        }
        //    }
        //}

        #endregion

        #region Public methods and accessors

        //public void UpdateTransform(Transform transform)
        //{
        //    this.transform = transform;
        //}

        public int NumPoints
        {
            get
            {
                return localPoints.Length;
            }
        }

        public Vector3 GetTangent(int index)
        {
            return MathUtility.TransformDirection(localTangents[index], transform, space);
        }

        public Vector3 GetNormal(int index)
        {
            return MathUtility.TransformDirection(localNormals[index], transform, space);
        }

        public Vector3 GetPoint(int index)
        {
            return MathUtility.TransformPoint(localPoints[index], transform, space);
        }

        /// Gets point on path based on distance travelled.
        //    public Vector3 GetPointAtDistance(float dst, EndOfPathInstruction endOfPathInstruction = EndOfPathInstruction.Loop)
        //    {
        //        float t = dst / length;
        //        return GetPointAtTime(t, endOfPathInstruction);
        //    }

        //    /// Gets forward direction on path based on distance travelled.
        //    public Vector3 GetDirectionAtDistance(float dst, EndOfPathInstruction endOfPathInstruction = EndOfPathInstruction.Loop)
        //    {
        //        float t = dst / length;
        //        return GetDirection(t, endOfPathInstruction);
        //    }

        //    /// Gets normal vector on path based on distance travelled.
        //    public Vector3 GetNormalAtDistance(float dst, EndOfPathInstruction endOfPathInstruction = EndOfPathInstruction.Loop)
        //    {
        //        float t = dst / length;
        //        return GetNormal(t, endOfPathInstruction);
        //    }

        //    /// Gets a rotation that will orient an object in the direction of the path at this point, with local up point along the path's normal
        //    public Quaternion GetRotationAtDistance(float dst, EndOfPathInstruction endOfPathInstruction = EndOfPathInstruction.Loop)
        //    {
        //        float t = dst / length;
        //        return GetRotation(t, endOfPathInstruction);
        //    }

        //    /// Gets point on path based on 'time' (where 0 is start, and 1 is end of path).
        //    public Vector3 GetPointAtTime(float t, EndOfPathInstruction endOfPathInstruction = EndOfPathInstruction.Loop)
        //    {
        //        var data = CalculatePercentOnPathData(t, endOfPathInstruction);
        //        return Vector3.Lerp(GetPoint(data.previousIndex), GetPoint(data.nextIndex), data.percentBetweenIndices);
        //    }

        //    /// Gets forward direction on path based on 'time' (where 0 is start, and 1 is end of path).
        //    public Vector3 GetDirection(float t, EndOfPathInstruction endOfPathInstruction = EndOfPathInstruction.Loop)
        //    {
        //        var data = CalculatePercentOnPathData(t, endOfPathInstruction);
        //        Vector3 dir = Vector3.Lerp(localTangents[data.previousIndex], localTangents[data.nextIndex], data.percentBetweenIndices);
        //        return MathUtility.TransformDirection(dir, transform, space);
        //    }

        //    /// Gets normal vector on path based on 'time' (where 0 is start, and 1 is end of path).
        //    public Vector3 GetNormal(float t, EndOfPathInstruction endOfPathInstruction = EndOfPathInstruction.Loop)
        //    {
        //        var data = CalculatePercentOnPathData(t, endOfPathInstruction);
        //        Vector3 normal = Vector3.Lerp(localNormals[data.previousIndex], localNormals[data.nextIndex], data.percentBetweenIndices);
        //        return MathUtility.TransformDirection(normal, transform, space);
        //    }

        //    /// Gets a rotation that will orient an object in the direction of the path at this point, with local up point along the path's normal
        //    public Quaternion GetRotation(float t, EndOfPathInstruction endOfPathInstruction = EndOfPathInstruction.Loop)
        //    {
        //        var data = CalculatePercentOnPathData(t, endOfPathInstruction);
        //        Vector3 direction = Vector3.Lerp(localTangents[data.previousIndex], localTangents[data.nextIndex], data.percentBetweenIndices);
        //        Vector3 normal = Vector3.Lerp(localNormals[data.previousIndex], localNormals[data.nextIndex], data.percentBetweenIndices);
        //        return Quaternion.LookRotation(MathUtility.TransformDirection(direction, transform, space), MathUtility.TransformDirection(normal, transform, space));
        //    }

        //    /// Finds the closest point on the path from any point in the world
        //    public Vector3 GetClosestPointOnPath(Vector3 worldPoint)
        //    {
        //        // Transform the provided worldPoint into VertexPath local-space.
        //        // This allows to do math on the localPoint's, thus avoiding the need to
        //        // transform each local vertexpath point into world space via GetPoint.
        //        Vector3 localPoint = MathUtility.InverseTransformPoint(worldPoint, transform, space);

        //        TimeOnPathData data = CalculateClosestPointOnPathData(localPoint);
        //        Vector3 localResult = Vector3.Lerp(localPoints[data.previousIndex], localPoints[data.nextIndex], data.percentBetweenIndices);

        //        // Transform local result into world space
        //        return MathUtility.TransformPoint(localResult, transform, space);
        //    }

        //    /// Finds the 'time' (0=start of path, 1=end of path) along the path that is closest to the given point
        //    public float GetClosestTimeOnPath(Vector3 worldPoint)
        //    {
        //        Vector3 localPoint = MathUtility.InverseTransformPoint(worldPoint, transform, space);
        //        TimeOnPathData data = CalculateClosestPointOnPathData(localPoint);
        //        return Mathf.Lerp(times[data.previousIndex], times[data.nextIndex], data.percentBetweenIndices);
        //    }

        //    /// Finds the distance along the path that is closest to the given point
        //    public float GetClosestDistanceAlongPath(Vector3 worldPoint)
        //    {
        //        Vector3 localPoint = MathUtility.InverseTransformPoint(worldPoint, transform, space);
        //        TimeOnPathData data = CalculateClosestPointOnPathData(localPoint);
        //        return Mathf.Lerp(cumulativeLengthAtEachVertex[data.previousIndex], cumulativeLengthAtEachVertex[data.nextIndex], data.percentBetweenIndices);
        //    }

        #endregion

        #region Internal methods

        #endregion

        // #region Internal methods
        //    /// For a given value 't' between 0 and 1, calculate the indices of the two vertices before and after t.
        //    /// Also calculate how far t is between those two vertices as a percentage between 0 and 1.
        //    TimeOnPathData CalculatePercentOnPathData(float t, EndOfPathInstruction endOfPathInstruction)
        //    {
        //        // Constrain t based on the end of path instruction
        //        switch (endOfPathInstruction)
        //        {
        //            case EndOfPathInstruction.Loop:
        //                // If t is negative, make it the equivalent value between 0 and 1
        //                if (t < 0)
        //                {
        //                    t += Mathf.CeilToInt(Mathf.Abs(t));
        //                }
        //                t %= 1;
        //                break;
        //            case EndOfPathInstruction.Reverse:
        //                t = Mathf.PingPong(t, 1);
        //                break;
        //            case EndOfPathInstruction.Stop:
        //                t = Mathf.Clamp01(t);
        //                break;
        //        }

        //        int prevIndex = 0;
        //        int nextIndex = NumPoints - 1;
        //        int i = Mathf.RoundToInt(t * (NumPoints - 1)); // starting guess

        //        // Starts by looking at middle vertex and determines if t lies to the left or to the right of that vertex.
        //        // Continues dividing in half until closest surrounding vertices have been found.
        //        while (true)
        //        {
        //            // t lies to left
        //            if (t <= times[i])
        //            {
        //                nextIndex = i;
        //            }
        //            // t lies to right
        //            else
        //            {
        //                prevIndex = i;
        //            }
        //            i = (nextIndex + prevIndex) / 2;

        //            if (nextIndex - prevIndex <= 1)
        //            {
        //                break;
        //            }
        //        }

        //        float abPercent = Mathf.InverseLerp(times[prevIndex], times[nextIndex], t);
        //        return new TimeOnPathData(prevIndex, nextIndex, abPercent);
        //    }

        //    /// Calculate time data for closest point on the path from given world point
        //    TimeOnPathData CalculateClosestPointOnPathData(Vector3 localPoint)
        //    {
        //        float minSqrDst = float.MaxValue;
        //        Vector3 closestPoint = Vector3.zero;
        //        int closestSegmentIndexA = 0;
        //        int closestSegmentIndexB = 0;

        //        for (int i = 0; i < localPoints.Length; i++)
        //        {
        //            int nextI = i + 1;
        //            if (nextI >= localPoints.Length)
        //            {
        //                if (isClosedLoop)
        //                {
        //                    nextI %= localPoints.Length;
        //                }
        //                else
        //                {
        //                    break;
        //                }
        //            }

        //            Vector3 closestPointOnSegment = MathUtility.ClosestPointOnLineSegment(localPoint, localPoints[i], localPoints[nextI]);
        //            float sqrDst = (localPoint - closestPointOnSegment).sqrMagnitude;
        //            if (sqrDst < minSqrDst)
        //            {
        //                minSqrDst = sqrDst;
        //                closestPoint = closestPointOnSegment;
        //                closestSegmentIndexA = i;
        //                closestSegmentIndexB = nextI;
        //            }

        //        }
        //        float closestSegmentLength = (localPoints[closestSegmentIndexA] - localPoints[closestSegmentIndexB]).magnitude;
        //        float t = (closestPoint - localPoints[closestSegmentIndexA]).magnitude / closestSegmentLength;
        //        return new TimeOnPathData(closestSegmentIndexA, closestSegmentIndexB, t);
        //    }

        //    public struct TimeOnPathData
        //    {
        //        public readonly int previousIndex;
        //        public readonly int nextIndex;
        //        public readonly float percentBetweenIndices;

        //        public TimeOnPathData(int prev, int next, float percentBetweenIndices)
        //        {
        //            this.previousIndex = prev;
        //            this.nextIndex = next;
        //            this.percentBetweenIndices = percentBetweenIndices;
        //        }
        //    }

        //#endregion

        //}

    }

}