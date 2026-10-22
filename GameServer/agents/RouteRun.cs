using System;
using System.Numerics;

namespace DOL.GS.Agents
{
    /// <summary>
    /// One run of the Agent's Character along a route, corner to corner, stepped the way the real 1.127 client in `Tests/Fixtures/Agents/v1127/run-turn-stop.tsv` runs (see the README there).
    /// Given the game loop time, it gives where the Character is, which way it faces, how fast it goes and which zone it's in. It touches nothing in the game.
    /// The speed ramps up from standing as the client's does, holds at the maximum speed round every corner, and falls to 0 so that the Character stops exactly at the last corner.
    /// Only X and Y count towards the distance and the speed, as in the server's speed check. Z is the floor under each position where there is one, and otherwise goes linearly between corners.
    /// Every position follows from the time since the run started, so a stall in the game loop never carries the Character past the end.
    /// </summary>
    public sealed class RouteRun
    {
        // In game units per second per second. Both captured starts gain about 53 units per second every 200 ms.
        public const float ACCELERATION = 265;

        // In game units per second per second. Both captured stops fall from 191 to about 100 in one 200 ms update and to 0 in the next, about 40 units on.
        public const float DECELERATION = 480;

        private readonly Vector3[] _corners;
        // How far each corner is along the route from the first, in X and Y.
        private readonly float[] _along;
        // The heading along each leg, the leg from corner `i` to corner `i + 1` at `i`.
        private readonly ushort[] _headings;
        private readonly Region _region;
        private readonly Func<Vector3, float?> _floor;
        private readonly float _length;
        private readonly ushort _startZoneId;
        private Profile _profile;
        private bool _stopped;

        /// <summary>
        /// Starts a run at `now` (the game loop time) through `corners`, all global, the first where the Character is, with the Character going at `speed` already, for example when a new run replaces one.
        /// `heading` is the one the Character has, which it keeps on a leg that goes straight up or down. `region` gives the zone at each position, from its static layout (ADR 0006).
        /// `floor` gives the Z of the ground under a position, or null where it doesn't know it.
        /// </summary>
        public RouteRun(Vector3[] corners, ushort heading, float speed, float maxSpeed, Region region, Func<Vector3, float?> floor, long now)
        {
            _corners = corners;
            _region = region;
            _floor = floor;
            _along = new float[corners.Length];
            _headings = new ushort[Math.Max(1, corners.Length - 1)];
            _headings[0] = heading;

            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 leg = new(corners[i].X - corners[i - 1].X, corners[i].Y - corners[i - 1].Y);
                _along[i] = _along[i - 1] + leg.Length();
                _headings[i - 1] = leg != Vector2.Zero ? HeadingAlong(leg) : i > 1 ? _headings[i - 2] : heading;
            }

            _length = _along[^1];
            _startZoneId = region.GetZone((int) corners[0].X, (int) corners[0].Y)?.ID ?? 0;
            _profile = new(now, 0, Math.Min(speed, maxSpeed), maxSpeed, _length);
        }

        // In a straight line from `start` to `target`, with no floor to follow.
        public RouteRun(Vector3 start, Vector3 target, ushort heading, float speed, float maxSpeed, Region region, long now) : this([start, target], heading, speed, maxSpeed, region, static _ => null, now) { }

        // Whether `Stop` ended the run short of the end.
        public bool IsStopped => _stopped;

        public Step At(long now)
        {
            (float distance, float speed, bool ended) = _profile.At(now);
            int leg = Leg(distance);
            Vector3 position;

            if (distance >= _length)
                position = _corners[^1];
            else
            {
                Vector3 from = _corners[leg];
                Vector3 to = _corners[leg + 1];
                float legLength = _along[leg + 1] - _along[leg];
                float fraction = legLength > 0 ? (distance - _along[leg]) / legLength : 1;
                position = new(from.X + (to.X - from.X) * fraction, from.Y + (to.Y - from.Y) * fraction, from.Z + (to.Z - from.Z) * fraction);
            }

            if (_floor(position) is float z)
                position.Z = z;

            return new(position, ZoneAt(position), _headings[leg], speed, ended, ended && !_stopped);
        }

        // Ends the run where the Character is at `now`, at once.
        public void Stop(long now)
        {
            (float distance, _, bool ended) = _profile.At(now);

            if (ended)
                return;

            _profile = new(now, distance, 0, 0, distance);
            _stopped = true;
        }

        // The server told the client a new maximum speed. From `now` the run speeds up to it, or drops to it at once if it's lower.
        public void ChangeMaxSpeed(float maxSpeed, long now)
        {
            (float distance, float speed, bool ended) = _profile.At(now);

            if (!ended)
                _profile = new(now, distance, Math.Min(speed, maxSpeed), maxSpeed, _length);
        }

        // The leg `distance` along the route falls on. A corner starts the leg after it, and the end is on the last leg.
        private int Leg(float distance)
        {
            int leg = 0;

            while (leg < _headings.Length - 1 && _along[leg + 1] <= distance)
                leg++;

            return leg;
        }

        // Where a position falls in the region's static zone layout. Outside every zone, the start's, since an unknown zone ID sends the client back to the character screen and a wrong one isn't checked.
        private ushort ZoneAt(Vector3 position)
        {
            return _region.GetZone((int) position.X, (int) position.Y)?.ID ?? _startZoneId;
        }

        // `Point2D.GetHeading`: heading 0 points towards +Y and grows clockwise.
        private static ushort HeadingAlong(Vector2 offset)
        {
            double heading = Math.Atan2(-offset.X, offset.Y) * Point2D.RADIAN_TO_HEADING;

            if (heading < 0)
                heading += 4096;

            return (ushort) ((int) heading % 4096);
        }

        /// <summary>
        /// Where the Character is on its run, what the client sends for it, and whether the run has ended (`Ended`), by arriving (`Arrived`) or by a stop.
        /// `Position` is global. `Heading` is 0 to 4095, without the flags a position update sets.
        /// </summary>
        public readonly record struct Step(Vector3 Position, ushort ZoneId, ushort Heading, float Speed, bool Ended, bool Arrived);

        /// <summary>
        /// How far along the line the Character is over time, from `Distance` at `Start` going at `Speed`: it speeds up to `MaxSpeed`, holds it, and slows down to stop at `End`.
        /// On a short run it starts slowing down before it reaches `MaxSpeed`. Going too fast to stop in time, it slows down harder, so it never passes `End`.
        /// </summary>
        private readonly struct Profile
        {
            private readonly long _start;
            private readonly float _distance;
            private readonly float _speed;
            private readonly float _end;
            // The highest speed it reaches, and how long, in seconds, it speeds up, holds it and slows down.
            private readonly float _peak;
            private readonly float _deceleration;
            private readonly float _accelerating;
            private readonly float _holding;
            private readonly float _decelerating;

            public Profile(long start, float distance, float speed, float maxSpeed, float end)
            {
                _start = start;
                _distance = distance;
                _speed = speed;
                _end = end;
                float remaining = end - distance;
                _deceleration = DECELERATION;

                // The speed at which speeding up and then slowing down covers exactly what's left.
                float peak = MathF.Sqrt((remaining + speed * speed / (2 * ACCELERATION)) / (1 / (2 * ACCELERATION) + 1 / (2 * DECELERATION)));

                if (peak < speed)
                {
                    // Too fast to stop in time at the usual rate.
                    peak = speed;
                    _deceleration = remaining > 0 ? speed * speed / (2 * remaining) : float.PositiveInfinity;
                }

                _peak = Math.Min(peak, maxSpeed);
                _accelerating = (_peak - speed) / ACCELERATION;
                float slowingDistance = _peak * _peak / (2 * _deceleration);
                float acceleratingDistance = (_peak * _peak - speed * speed) / (2 * ACCELERATION);
                _holding = _peak > 0 ? Math.Max(0, remaining - acceleratingDistance - slowingDistance) / _peak : 0;
                _decelerating = _peak > 0 ? _peak / _deceleration : 0;
            }

            // A run that can't go anywhere, for example with a maximum speed of 0, waits where it is without ending.
            public (float Distance, float Speed, bool Ended) At(long now)
            {
                if (_distance >= _end)
                    return (_end, 0, true);

                if (_peak <= 0)
                    return (_distance, 0, false);

                float t = Math.Max(0, now - _start) / 1000f;

                if (t < _accelerating)
                    return (Math.Min(_end, _distance + _speed * t + ACCELERATION * t * t / 2), _speed + ACCELERATION * t, false);

                float accelerated = _distance + (_peak * _peak - _speed * _speed) / (2 * ACCELERATION);
                t -= _accelerating;

                if (t < _holding)
                    return (Math.Min(_end, accelerated + _peak * t), _peak, false);

                float held = accelerated + _peak * _holding;
                t -= _holding;

                if (t < _decelerating)
                    return (Math.Min(_end, held + _peak * t - _deceleration * t * t / 2), _peak - _deceleration * t, false);

                return (_end, 0, true);
            }
        }
    }
}
