namespace CityGen.Util
{
    public abstract class FieldIntegrator
    {
        /// The tensor field to integrate.
        protected TensorField TensorField;

        /// The streamline parameters.
        protected StreamlineGenerator.Parameters StreamLineParams;

        /// Subclass constructor.
        protected FieldIntegrator(TensorField tensorField, StreamlineGenerator.Parameters streamLineParams)
        {
            TensorField = tensorField;
            StreamLineParams = streamLineParams;
        }

        /// Integrate a point.
        public abstract Vector2 Integrate(Vector2 pt, bool major);

        /// Sample a field vector.
        protected Vector2 SampleFieldVector(Vector2 pt, bool major)
        {
            var tensor = TensorField.SamplePoint(pt);
            return major ? tensor.Major : tensor.Minor;
        }

        /// Whether or not a point is on land.
        protected bool IsPointOnLand(Vector2 pt)
        {
            return TensorField.IsPointOnLand(pt);
        }
    }

    /// Simple euler integrator.
    public class EulerIntegrator : FieldIntegrator
    {
        /// Constructor.
        public EulerIntegrator(TensorField tensorField, StreamlineGenerator.Parameters streamLineParams)
            : base(tensorField, streamLineParams)
        {
            
        }

        /// Integrate a point.
        public override Vector2 Integrate(Vector2 pt, bool major)
        {
            return SampleFieldVector(pt, major) * StreamLineParams.DStep;
        }
    }

    /// Runge-Kutta 4 integrator.
    public class RungeKutta4Integrator : FieldIntegrator
    {
        /// Constructor.
        public RungeKutta4Integrator(TensorField tensorField, StreamlineGenerator.Parameters streamLineParams)
            : base(tensorField, streamLineParams)
        {
            
        }

        /// Integrate a point.
        public override Vector2 Integrate(Vector2 pt, bool major)
        {
            var k1 = SampleFieldVector(pt, major);
            var k23 = SampleFieldVector(pt + new Vector2(StreamLineParams.DStep / 2), major);
            var k4 = SampleFieldVector(pt + new Vector2(StreamLineParams.DStep), major);

            return (k1 + (k23 * 4) + k4) * (StreamLineParams.DStep / 6);
        }
    }
}