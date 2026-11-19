using Game.UI.Foundation;
using UnityEngine.UIElements;

namespace Game.UI
{
    public sealed class MenuDocument : DocumentView
    {
        public MenuViewModel ViewModel => (MenuViewModel)Model;

        protected override void BindElements()
        {
            MenuViewModel model = ViewModel;
            Field("nickname", model.Nickname, model.SetNickname);
            Field("address", model.Address, model.SetAddress, model.Join);
            Text("message", model.Message);
            Text("arenaMode", model.ArenaModeText);
            Visible("cancel", model.CanCancel);
            Visible("disconnected", model.HostDisconnected);
            Visible("homePage", model.HomeVisible);
            Visible("createPage", model.CreateVisible);
            Visible("joinPage", model.JoinVisible);
            Visible("sessionSettings", model.SettingsVisible);
            Text("selectedGame", model.SelectedGameText);

            foreach (string name in new[]
            {
                "openCreate", "openJoin", "create", "join", "search", "nickname", "address",
                "training", "arenaMode", "servers", "selectFlat", "selectTableAR", "selectRoomAR"
            })
            {
                Enabled(name, model.CanStart);
            }

            Button("create", model.CreateGame);
            Button("join", model.Join);
            Button("search", model.Search);
            Button("cancel", model.Cancel);
            Button("training", model.Train);
            Button("arenaMode", model.ToggleArenaMode);
            Button("acknowledge", model.AcknowledgeDisconnect);
            Button("openCreate", model.OpenCreate);
            Button("openJoin", model.OpenJoin);
            Button("back", model.Back);
            Button("quit", model.Quit);
            Button("selectFlat", model.SelectFlatDuel);
            Button("selectTableAR", model.SelectTableDuel);
            Button("selectRoomAR", model.SelectRoom);
            string[] choices = { "selectFlat", "selectTableAR", "selectRoomAR" };
            Observe(model.SelectedOption, selected =>
            {
                for (int i = 0; i < choices.Length; i++)
                {
                    Element<Button>(choices[i]).EnableInClassList("selected", selected == i);
                }
            });

            ScrollView list = Element<ScrollView>("servers");
            Observe(model.Servers, servers =>
            {
                list.Clear();

                for (int i = 0; i < servers.Count; i++)
                {
                    int index = i;
                    var row = new Button(() => model.JoinServer(index))
                    {
                        text = servers[i]
                    };
                    row.AddToClassList("server-row");
                    list.Add(row);
                }
            });
        }
    }
}
