namespace Metin2.Gameplay.Flow
{
    /// <summary>
    /// Lifecycle of a <see cref="GameFlow"/> session.
    /// </summary>
    public enum GameFlowState
    {
        /// <summary>Not started (or a failed login returned here).</summary>
        Idle = 0,

        /// <summary>Auth hop complete: the login key is held.</summary>
        LoggedIn = 1,

        /// <summary>Channel hop complete: slots and empire are held.</summary>
        ChannelReady = 2,

        /// <summary>Character selected, loading bundle consumed (113/16/76/items).</summary>
        CharacterReady = 3,

        /// <summary>ENTERGAME complete (time + channel received); event pump can run.</summary>
        InWorld = 4
    }
}
