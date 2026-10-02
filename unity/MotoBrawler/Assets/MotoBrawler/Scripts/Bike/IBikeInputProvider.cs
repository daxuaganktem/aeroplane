namespace MotoBrawler.Bike
{
    /// <summary>
    /// One frame of rider intent. The BikeController only ever sees this struct, so a
    /// human (keyboard / touch / tilt), an AI rider or a replay can all drive the same bike.
    /// </summary>
    public struct BikeInputState
    {
        /// <summary>-1 = full left, +1 = full right.</summary>
        public float steer;

        /// <summary>0..1</summary>
        public float throttle;

        /// <summary>0..1. Held while stopped = reverse.</summary>
        public float brake;

        public static readonly BikeInputState None = default;
    }

    /// <summary>
    /// Anything that can ride a bike. Put a component implementing this on the bike root
    /// (PlayerBikeInput for humans; later an AIRiderInput) or call BikeController.SetInputProvider.
    /// </summary>
    public interface IBikeInputProvider
    {
        BikeInputState ReadInput();
    }
}
