namespace Unity.AutoLOD
{
    public interface IPreferences
    {
        /// <summary>
        /// Callback from AutoLOD preferences GUI to show GUI options related to a simplifier / batcher.
        /// Settings should be saved here as well and also utilized by the simplifier / batcher.
        /// </summary>
        void OnPreferencesGUI();
    }
}
