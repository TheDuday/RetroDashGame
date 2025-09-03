namespace MauiGameAttempt8
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Navigation.PushAsync(new NavigationPage( new TitlePage()));
        }
        protected override bool OnBackButtonPressed()
        {
            // Prevent back navigation triggered by Spacebar or Enter
            return true; // This disables all back navigation
        }
    }
}
