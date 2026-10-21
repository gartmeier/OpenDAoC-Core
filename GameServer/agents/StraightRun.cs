using System;
using System.Numerics;

namespace DOL.GS.Agents
{
    /// <summary>
    /// One run of the Agent's Character in a straight line to a target, stepped the way the real 1.127 client in `Tests/Fixtures/Agents/v1127/run-turn-stop.tsv` runs (see the README there).
    /// Given the game loop time, it gives where the Character is, which way it faces, how fast it goes and which zone it's in. It touches nothing in the game.
    /// The speed ramps up from standing as the client's does, holds at the maximum speed, and falls to 0 so that the Character stops exactly at the target.
    /// Only X and Y count towards the distance and the speed, as in the server's speed check. Z goes linearly from the start's to the target's, since there's no terrain to follow.
    /// Every position follows from the time since the run started, so a stall in the game loop never carries the Character past the target.
    /// </summary>
    public sealed class StraightRun
    {
        // In game units per second per second. Both captured starts gain about 53 units per second every 200 ms.
        public const float ACCELERATION = 265;

        // In game units per second per second. Both captured stops fall from 191 to about 100 in one 200 ms update and to 0 in the next, about 40 units on.
        public const float DECELERATION = 480;

        private readonly Vector3 _start;
        private readonly Vector3 _target;
        private readonly Region _region;
        // Along the line from the start, in X and Y.
        private readonly float _length;
        private readonly Vector2 _direction;
        private readonly ushort _startZoneId;
        private Profile _profile;
        private bool _stopped;

        /// <summary>
        /// Starts a run at `now` (the game loop time) from `start` to `target`, both global, with the Character going at `speed` already, for example when a new run replaces one.
        /// `heading` is the one the Character has, which it keeps if the target is straight above or below it. `region` gives the zone at each position, from its static layout (ADR 0006).
        /// </summary>
        public StraightRun(Vector3 start, Vector3 target, ushort heading, float speed, float maxSpeed, Region region, long now)
        {
            _start = start;
            _target = target;
            _region = region;
            Vector2 offset = new(target.X - start.X, target.Y - start.Y);
            _length = offset.Length();
            _direction = _length > 0 ? offset / _length : Vector2.Zero;
            Heading = _length > 0 ? HeadingAlong(offset) : heading;
            _startZoneId = region.GetZone((int) start.X, (int) start.Y)?.ID ?? 0;
            _profile = new(now, 0, Math.Min(speed, maxSpeed), maxSpeed, _length);
        }

        public Vector3 Target => _target;

        // Towards the target, 0 to 4095, as the server measures headings (`Point2D.GetHeading`). It doesn't change during a run.
        public ushort Heading { get; }

        // Whether `Stop` ended the run short of the target.
        public bool IsStopped => _stopped;

        public Step At(long now)
        {
            (float distance, float speed, bool ended) = _profile.At(now);
            float fraction = _length > 0 ? distance / _length : 1;
            Vector2 flat = new Vector2(_start.X, _start.Y) + _direction * distance;
            Vector3 position = fraction >= 1 ? _target : new(flat.X, flat.Y, _start.Z + (_target.Z - _start.Z) * fraction);
            return new(position, ZoneAt(position), Heading, speed, ended, ended && !_stopped);
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
