namespace CPPRogue.Game
{
    /// <summary>
    /// 单局生命周期旗标：死亡弹窗在场（Over = true）时 ESC / 暂停编辑全部失效，
    /// TickDriver 不再调度新 tick；重开（GameBootstrap.Restart）时复位。
    /// </summary>
    public static class GameRun
    {
        public static bool Over;
    }
}
