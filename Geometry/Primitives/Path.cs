﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// Describes a set of points connected sequentially, i.e. a polyline.  Exposes events for changes to the path.
    /// </summary>
    public class Path : IPolyLine2D, System.Collections.Specialized.INotifyCollectionChanged, IEquatable<IPolyLine2D>, IEquatable<ILineSegment2D>
    {

        public delegate void LoopChangedEventHandler(object sender, bool HasLoop);

        /// <summary>
        /// Fires an event when a loop in the path is found or removed
        /// </summary>
        public event LoopChangedEventHandler OnLoopChanged;

        private void FireOnLoopChangedEvent(bool HasLoop)
        {
            //Trace.WriteLine(string.Format("FireOnLoopChangedEvent: {0}", HasLoop));

            this.OnLoopChanged?.Invoke(this, HasLoop);
        }

        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler OnPathChanged;

        event NotifyCollectionChangedEventHandler System.Collections.Specialized.INotifyCollectionChanged.CollectionChanged
        {
            add
            {
                this.OnPathChanged += value;
            }
            remove
            {
                this.OnPathChanged -= value;
            }
        }

        private void FireOnPathChangedEvent(NotifyCollectionChangedEventArgs e)
        {
            this.OnPathChanged?.Invoke(this, e);
        }

        public List<GridVector2> Points = new List<GridVector2>();

        public double Length => Segments.Sum(s => s.Length);

        private readonly uint _SimplifiedPathInterpolations = 5;
        /// <summary>
        /// Sets how far from the actual path is a simplified path is allowed to stray.
        /// </summary>
        private double _SimplifiedPathTolerance = 1.0;

        public double SimplifiedPathTolerance
        {
            get => _SimplifiedPathTolerance;
            set
            {
                if (value == _SimplifiedPathTolerance)
                    return;

                _SimplifiedPathTolerance = value;
                _SimplifiedPath = null;
                _SimplifiedLoop = null;
                _SimplifiedLoopSegments = null;
            }
        }


        private GridVector2[] _SimplifiedPath;
        public GridVector2[] SimplifiedPath
        {
            get
            {
                if (_SimplifiedPath == null)
                {
                    try
                    {
                        _SimplifiedPath = CatmullRomControlPointSimplification.IdentifyControlPoints(this.Points, SimplifiedPathTolerance, false, _SimplifiedPathInterpolations).ToArray();
                    }
                    catch (ArgumentException)
                    {
                        Trace.WriteLine("Could not simplify path, trying tighter tolerance...");
                        try
                        {
                            _SimplifiedPath = CatmullRomControlPointSimplification.IdentifyControlPoints(this.Points, SimplifiedPathTolerance / 2.0, false, _SimplifiedPathInterpolations).ToArray();
                        }
                        catch (ArgumentException)
                        {
                            Trace.WriteLine("Could not simplify path, using original path...");
                            _SimplifiedPath = this.Points.ToArray();
                        }
                    }
                }

                return _SimplifiedPath;
            }
        }

        public GridLineSegment NewestSegment
        {
            get
            {
                int count = Points.Count;
                return new GridLineSegment(Points[count - 1], Points[count - 2]);
            }
        }

        /// <summary>
        /// Segments are ordered so that A is the newer control point and B is the older control point in the path
        /// </summary>
        private readonly List<GridLineSegment> _Segments = new List<GridLineSegment>();
        public IReadOnlyList<GridLineSegment> Segments => _Segments;

        /// <summary>
        /// True if the path has at least two points
        /// </summary>
        public bool HasSegment => Points.Count >= 2;

        public bool HasSelfIntersection => _Loop != null;

        /// <summary>
        /// Segments are ordered so that A is the newer control point and B is the older control point in the path
        /// </summary>
        private GridVector2[] _Loop = null;

        /// <summary>
        /// Returns the line segments composing the first loop described by the path, or null if no self-intersection exists
        /// </summary>
        public GridVector2[] Loop => _Loop;

        /// <summary>
        /// Segments are ordered so that A is the newer control point and B is the older control point in the path
        /// </summary>
        private GridLineSegment[] _LoopSegments = null;

        /// <summary>
        /// Returns the line segments composing the first loop described by the path, or null if no self-intersection exists
        /// </summary>
        public GridLineSegment[] LoopSegments => _LoopSegments;


        /// <summary>
        /// Segments are ordered so that A is the newer control point and B is the older control point in the path
        /// </summary>
        private GridVector2[] _SimplifiedLoop = null;

        /// <summary>
        /// Returns the line segments composing the first loop described by the path, or null if no self-intersection exists
        /// </summary>
        public GridVector2[] SimplifiedFirstLoop
        {
            get
            {
                if (_SimplifiedLoop == null)
                {
                    if (HasSelfIntersection)
                        this._SimplifiedLoop = this._Loop.IdentifyControlPoints(this.SimplifiedPathTolerance, true, _SimplifiedPathInterpolations).EnsureClosedRing().ToArray();
                    else
                        return null;
                }

                return _SimplifiedLoop;
            }
        }


        /// <summary>
        /// Segments are ordered so that A is the newer control point and B is the older control point in the path
        /// </summary>
        private GridLineSegment[] _SimplifiedLoopSegments = null;

        /// <summary>
        /// Returns the line segments composing the first loop described by the path, or null if no self-intersection exists
        /// </summary>
        public GridLineSegment[] SimplifiedLoopSegments
        {
            get
            {
                if (_SimplifiedLoopSegments == null)
                {
                    if (HasSelfIntersection)
                    {
                        _SimplifiedLoopSegments = this._SimplifiedLoop.ToLineSegments();
                    }
                    else
                    {
                        return null;
                    }
                }

                return _SimplifiedLoopSegments;
            }
        }

        public Path()
        {

        }

        public void Push(GridVector2 p)
        {
            bool HasLoop = this.HasSelfIntersection;
            Push_NoEvent(p);
            bool HasLoopAfterPush = this.HasSelfIntersection;

            FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, p, 0));

            if (HasLoop != HasLoopAfterPush)
            {
                FireOnLoopChangedEvent(HasLoopAfterPush);
            }
        }

        private void Push_NoEvent(GridVector2 p)
        {
            bool FoundLoop = CheckForSelfIntersectionBeforePush(p);  //If we don't already have a self intersection detected, check if this creates one. Do this before adding a new segment

            //Add the new line segment to our list
            if (this.Points.Count > 0)
            {
                GridVector2 lastPoint = this.Peek();
                GridLineSegment newSegment = new GridLineSegment(p, lastPoint);
#if DEBUG
                if (_Segments.Count > 0)
                {
                    System.Diagnostics.Debug.Assert(_Segments.Last().A == lastPoint); //Ensure our line segments are contiguous
                }
#endif
                _Segments.Add(newSegment);
            }

            this.Points.Add(p);
            _SimplifiedPath = null;  //TODO: This could be optimized to only calculate the new segment

            //Make sure we have the right number of segments for points in the path
            System.Diagnostics.Debug.Assert(_Segments.Count == this.Points.Count - 1);
        }

        public GridVector2 Pop()
        {
            bool HasLoop = this.HasSelfIntersection;
            GridVector2 removed = this.Pop_NoEvent();
            bool HasLoopAfterPush = this.HasSelfIntersection;

            FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, 0));

            if (HasLoop != HasLoopAfterPush)
            {
                FireOnLoopChangedEvent(HasLoopAfterPush);
            }

            return removed;
        }

        private GridVector2 Pop_NoEvent()
        {
            CheckForSelfIntersectionLossBeforePop();

            GridVector2 p = this.Points.First();
            this.Points.RemoveAt(this.Points.Count - 1);

            if (this._Segments.Count > 0)
            {
                _Segments.RemoveAt(this._Segments.Count - 1);
            }

            _SimplifiedPath = null;  //TODO: This could be optimized to only calculate the new segment

            //Make sure we have the right number of segments for points in the path
            System.Diagnostics.Debug.Assert(_Segments.Count == this.Points.Count - 1);
            return p;
        }

        public GridVector2 Peek()
        {
            return this.Points[this.Points.Count - 1];
        }

        public void Clear()
        {
            bool HadLoop = this.HasSelfIntersection;

            Points = new List<GridVector2>();
            _SimplifiedPath = null;
            ResetLoop();
            FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            if (HadLoop)
            {
                FireOnLoopChangedEvent(HasLoop: false);
            }
        }

        /// <summary>
        /// Erase points up to and including the passed index
        /// </summary>
        /// <param name="iDeletePoint"></param>
        /// <returns>True if part of the path was erased</returns>
        public bool Erase(int iDeletePoint)
        {
            if (iDeletePoint >= 0)
            {
                bool HadLoop = this.HasSelfIntersection;

                int NumExpectedToDelete = Points.Count - iDeletePoint;
                GridVector2[] removedEntries = new GridVector2[NumExpectedToDelete];
                Points.CopyTo(iDeletePoint, removedEntries, 0, NumExpectedToDelete);

                int NumDeleted = 0;

                while (NumDeleted < NumExpectedToDelete)
                //while (iDeletePoint >= 0)
                {
                    this.Pop_NoEvent();
                    NumDeleted++;
                }
                System.Diagnostics.Debug.Assert(NumDeleted == removedEntries.Length);

                FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedEntries, iDeletePoint));

                if (HadLoop != this.HasSelfIntersection)
                {
                    FireOnLoopChangedEvent(this.HasSelfIntersection);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Erase the path to the closest vertex to the passed point
        /// </summary>
        /// <param name="p"></param>
        /// <returns>True if part of the path was erased</returns>
        public bool Erase(GridVector2 input)
        {
            double[] distances = Points.Select(v => GridVector2.Distance(v, input)).ToArray();
            double min_distance = distances.Min();

            int iDeletePoint = Array.IndexOf(distances, distances.Min());

            return this.Erase(iDeletePoint);
        }

        /// <summary>
        /// Replace the top of the path with the new value
        /// </summary>
        /// <param name="p"></param>
        public void Replace(GridVector2 p)
        {
            if (p == this.Peek())
                return; //Do nothing if the points are the same

            bool HadLoop = this.HasSelfIntersection;

            GridVector2 oldValue = this.Pop_NoEvent();
            bool HadLoopAfterPop = this.HasSelfIntersection;
            this.Push_NoEvent(p);

            bool HasLoopAfterPush = this.HasSelfIntersection;

            FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, p, oldValue, Points.Count - 1));

            //Check if we added a loop, or if we removed a loop and then re-added it.
            if (HadLoop != HasLoopAfterPush || HadLoop != HadLoopAfterPop)
            {
                FireOnLoopChangedEvent(HasLoopAfterPush);
            }
        }

        /// <summary>
        /// Resets the lopos stored in this path
        /// </summary>
        private void SetLoop(List<GridVector2> loopPoints)
        {
            this._Loop = loopPoints.EnsureClosedRing().ToArray();
            this._LoopSegments = this._Loop.ToLineSegments();
            this._SimplifiedLoop = null; //Recalculated on demand
            this._SimplifiedLoopSegments = null; //Recalculated on demand
            System.Diagnostics.Debug.Assert(_LoopSegments[0].A == _LoopSegments[_LoopSegments.Length - 1].B);
        }

        /// <summary>
        /// Resets the lopos stored in this path
        /// </summary>
        private void ResetLoop()
        {
            this._Loop = null;
            this._LoopSegments = null;
            this._SimplifiedLoop = null;
            this._SimplifiedLoopSegments = null;
        }


        /// <summary>
        /// If we add the passed point will it intersect our path?
        /// </summary>
        /// <param name="new_point"></param>
        /// <returns>True if a NEW loop was found</returns>
        public bool CheckForSelfIntersectionBeforePush(in GridVector2 p)
        {
            if (HasSelfIntersection)
            {
                return false;
            }

            //Need at least four points for a self intersection
            if (this.Points.Count < 3)
            {
                return false;
            }

            this._LoopSegments = null;
            this._SimplifiedLoopSegments = null;
            GridLineSegment newSegment = new GridLineSegment(p, this.Peek());
            List<GridLineSegment> loopSegments = new List<GridLineSegment>(this._Segments.Count);

            List<GridVector2> loopPoints = new List<GridVector2>();

            //This function looks odd because the lines are reversed. A is closer to the most recently placed point in the path

            int IntersectionCount = 0;
            for (int iPathLine = 0; iPathLine < this._Segments.Count; iPathLine++)
            {
                GridLineSegment path_line = this._Segments[iPathLine];
                if (newSegment.Intersects(path_line, out GridVector2 intersection))
                {
                    IntersectionCount += 1;

                    if (IntersectionCount == 1)
                    {
                        //Add the line from the intersection to the near point of the path
                        if (path_line.IsEndpoint(intersection))
                        {
                            if (path_line.B == intersection) //The entire line belongs in the loop
                            {
                                //loop_segment = path_line;
                                loopPoints.Add(intersection);
                                loopPoints.Add(path_line.A);
                            }
                            else //We intersected the end of this line and none of it is in the loop.  We should add the next segment in the path instead.
                            {
                                IntersectionCount -= 1;
                                continue;
                            }
                        }
                        else
                        {
                            //The intersection is along the line, add the intersection point and the end of our line
                            loopPoints.Add(intersection);
                            loopPoints.Add(path_line.A);
                        }

                        //loopSegments.Add(loop_segment); //Start populating the loop
                    }
                    else if (IntersectionCount == 2)
                    {
                        //We found the closing point of the loop.
                        loopPoints.Add(intersection);
                        break;
                        //Add the bit from the path point to the intersection
                        /*
                        System.Diagnostics.Debug.Assert(path_line.IsEndpoint(intersection));
                        if (path_line.IsEndpoint(intersection))
                        {
                            loopPoints.Add(path_line.B);
                            if (path_line.B != intersection)
                                loopPoints.Add(intersection);

                            break;
                            /*
                            if (path_line.B == intersection) //The loop ends at the start of our line, do nothing
                            {
                                break;
                            }
                            else //We intersected the end of this line and it entirely belongs in the loop.
                            {
                                loopSegments.Add(path_line);
                                continue;
                            }
                        }
                        else
                        {
                            //Add the part from the start of our line to the intersection
                            loop_segment = new GridLineSegment(path_line.B, intersection);
                            loopSegments.Add(loop_segment);
                            break;
                        */
                        /*
                        }
                        */

                    }
                }
                else if (IntersectionCount == 1)
                {
                    //No intersection, just add this line's endpoint to the list of points in the loop
                    loopPoints.Add(path_line.A);
                }
            }

            if (IntersectionCount == 2)
            {
                SetLoop(loopPoints);
                return true;
            }
            else
            {
                ResetLoop();
                return false;
            }

            //List<GridLineSegment> intersectingSegments = newSegment.Intersections(this.Segments, false, out GridVector2[] intersectionPoints);
            //intersectionPoints = intersectionPoints.Where(p => newSegment.B != p).ToArray(); //We know that the most recent point in the path will share an endpoint, so remove these from results
            //intersectingSegments = intersectingSegments.Where(s => s != this.NewestSegent).ToList();
            //if (intersectionPoints.Length > 0)
            //{
            //    System.Diagnostics.Debug.Assert(intersectionPoints.Length == 1); //We should only find one self intersection, then stop looking

            //    this.FirstSelfIntersectingSegmentPair = new GridLineSegment[] { intersectingSegments[0], newSegment };

            //    return true;
            //}

            //return false;
        }

        /// <summary>
        /// If we pop the top point will it remove an existing self-intersection?
        /// </summary>
        /// <param name="new_point"></param>
        /// <returns>True if popping the point will break an existing loop</returns>
        private bool CheckForSelfIntersectionLossBeforePop()
        {
            if (false == this.HasSegment)
            {
                return false;
            }

            GridLineSegment lostSegment = this.NewestSegment;
            if (false == this.HasSelfIntersection)
            {
                return false;
            }

            //If we are popping one of the line segments in the pair, then clear the self intersection array
            if (lostSegment.B == _Loop[_Loop.Length - 2])
            {
                ResetLoop();
                return true;
            }

            return false;
        }

        public double Distance(in GridVector2 p)
        {
            if (this.Points.Count == 0)
            {
                throw new ArgumentException("No points in path to calculate distance");
            }
            else if (this.Points.Count == 1)
            {
                return GridVector2.Distance(this.Points[0], in p);
            }
            else
            {
                GridVector2 pnt = p;
                return this.Segments.Min(seg => seg.DistanceToPoint(pnt));
            }
        }

        #region IPolyLine2D
        public GridRectangle BoundingBox
        {
            get
            {
                double MinX = Points.Min(p => p.X);
                double MaxX = Points.Max(p => p.X);
                double MinY = Points.Min(p => p.Y);
                double MaxY = Points.Max(p => p.Y);

                return new GridRectangle(MinX, MaxX, MinY, MaxY);
            }
        }


        IReadOnlyList<ILineSegment2D> IPolyLine2D.LineSegments
        {
            get
            {
                List<ILineSegment2D> listSegments = new List<ILineSegment2D>(this.Points.Count - 1);

                for (int i = 0; i < Points.Count - 1; i++)
                {
                    listSegments.Add(new GridLineSegment(Points[i], Points[i + 1]));
                }

                return listSegments;
            }
        }

        IReadOnlyList<IPoint2D> IPolyLine2D.Points => this.Points.Select(p => (IPoint2D)p).ToList();

        public ShapeType2D ShapeType => ShapeType2D.POLYLINE;

        public double Area => throw new ArgumentException("No area for Polyline");

        GridVector2 IShape2D.Centroid => GridVector2.Average(Segments.Select(s => s.PointAlongLine(0.5)));

        bool IShape2D.Contains(in IPoint2D p)
        {
            IPoint2D pnt = p;
            return this.Segments.Any(line => line.Contains(pnt));
        }

        bool IShape2D.Intersects(in IShape2D shape)
        {
            IShape2D shp = shape;
            return this.Segments.Any(line => line.Intersects(shp));
        }

        IShape2D IShape2D.Translate(in IPoint2D offset)
        {
            List<IPoint2D> translatedPoints = new List<Geometry.IPoint2D>(this.Points.Count);

            var X = offset.X;
            var Y = offset.Y;
            translatedPoints = this.Points.Select(p => new GridVector2(p.X + X, p.Y + Y)).Cast<IPoint2D>().ToList();

            return new GridPolyline(translatedPoints);
        } 

        public bool Equals(IShape2D other)
        {
            if (other is IPolyLine2D otherPolyline)
            {
                if (this.Points.Count != otherPolyline.Points.Count)
                    return false;

                for (int i = 0; i < this.Points.Count; i++)
                {
                    if (false == this.Points[i].Equals(otherPolyline.Points[i]))
                        return false;
                }

                return true;
            }

            return false;
        }

        public bool Equals(ILineSegment2D other)
        {
            if (this.Points.Count != 2)
                return false;

            return (Points[0].Equals(other.A) && Points[1].Equals(other.B)) ||
                   (Points[1].Equals(other.A) && Points[0].Equals(other.B));
        }

        public bool Equals(IPolyLine2D other)
        { 
            if (this.Points.Count != other.Points.Count)
                return false;

            for (int i = 0; i < this.Points.Count; i++)
            {
                if (false == this.Points[i].Equals(other.Points[i]))
                    return false;
            }

            return true;  
        }

        #endregion

    }
}
