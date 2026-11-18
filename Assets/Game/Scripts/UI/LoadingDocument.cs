using Game.UI.Foundation;
using UnityEngine.UIElements;

namespace Game.UI
{
    public sealed class LoadingDocument : DocumentView
    {
        public LoadingViewModel ViewModel => (LoadingViewModel)Model;

        protected override void BindElements()
        {
            LoadingViewModel model = ViewModel;
            Text("description", model.Description);
            Progress("progress", model.Progress);
            Observe(model.ProgressText, value => Element<ProgressBar>("progress").title = value);
            Visible("loading", model.Visible);
            Observe(model.Transparent, value => Element<VisualElement>("loading").EnableInClassList("transparent", value));
        }
    }
}
