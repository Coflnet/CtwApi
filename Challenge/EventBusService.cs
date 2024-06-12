public class EventBusService
{
    public event EventHandler<ImageUploadEvent> ImageUploaded;
    public void OnImageUploaded(ImageUploadEvent e)
    {
        ImageUploaded?.Invoke(this, e);
    }

}
