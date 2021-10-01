using UnityEngine;

namespace PartMusic
{
    public class PartMusic : PartModule
    {
        [KSPField]
        public string AudioFile = "";

        private AudioClip m_MusicClip;
        public AudioClip MusicClip
        {
            get
            {
                if (m_MusicClip is null)
                {
                    m_MusicClip = GameDatabase.Instance.GetAudioClip(AudioFile);
                    if (m_MusicClip is null) Debug.Log("PartMusic cannot find "+ AudioFile);
                }
                return m_MusicClip;
            }
        }
        private AudioSource player;

        [UI_Toggle(scene = UI_Scene.All,enabledText = "#autoLOC_PartMusic_Playing", disabledText = "#autoLOC_PartMusic_Stopped")]
        [KSPField(guiName = "#autoLOC_PartMusic", isPersistant = true,guiActive =true)]
        public bool isPlayingMusic;

        void Start()
        {
            player = part.gameObject.AddComponent<AudioSource>();
            player.clip = MusicClip;
            player.loop = true;
        }

        void Update()
        {
            if (isPlayingMusic && !player.isPlaying)
            {
                player.Play();
            }
            if (!isPlayingMusic && player.isPlaying)
            {
                player.Stop();
            }
        }
    }
}
