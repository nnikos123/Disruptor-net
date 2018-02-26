namespace Disruptor
{
    public interface IBatchEventProcessor<T> : IEventProcessor
    {
        /// <summary>
        /// Set a new <see cref="IExceptionHandler{T}"/> for handling exceptions propagated out of the <see cref="BatchEventProcessor{T}"/>
        /// </summary>
        /// <param name="exceptionHandler">exceptionHandler to replace the existing exceptionHandler.</param>
        void SetExceptionHandler(IExceptionHandler<T> exceptionHandler);
    }
}
