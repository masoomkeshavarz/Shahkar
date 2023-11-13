namespace ShahkarAPI.Models
{
    public class ShahkarResult<T>
    {
        public int IsSuccess { get; set; } //0=error and 1=success
        public T? Data { get; set; }
    }
}
