namespace VanAn.ShopERP.Services
{
    public interface IErrorNotificationService
    {
        Task ShowError(string message);
        Task ShowWarning(string message);
        Task ShowSuccess(string message);
        Task ShowInfo(string message);
    }

    public class ErrorNotificationService : IErrorNotificationService
    {
        public async Task ShowError(string message)
        {
            // Implementation for showing error notifications
            // This could integrate with a toast notification system
            Console.WriteLine($"ERROR: {message}");
            await Task.CompletedTask;
        }

        public async Task ShowWarning(string message)
        {
            Console.WriteLine($"WARNING: {message}");
            await Task.CompletedTask;
        }

        public async Task ShowSuccess(string message)
        {
            Console.WriteLine($"SUCCESS: {message}");
            await Task.CompletedTask;
        }

        public async Task ShowInfo(string message)
        {
            Console.WriteLine($"INFO: {message}");
            await Task.CompletedTask;
        }
    }
}
