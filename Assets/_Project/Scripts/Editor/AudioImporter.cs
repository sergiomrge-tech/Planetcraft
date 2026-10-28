using UnityEditor;
using UnityEngine;

namespace Planetcraft.EditorTools
{
    // Importação dos áudios gerados por tools/gen_audio.py: trilha em streaming (não ocupa memória), loops de
    // ambiência comprimidos em memória e cantos de ave curtos descomprimidos (disparo sem atraso).
    public class AudioImporter : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/_Project/Resources/Audio/")) return;
            var importer = (UnityEditor.AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            if (assetPath.Contains("/Music/")) settings.loadType = AudioClipLoadType.Streaming;
            else if (assetPath.Contains("/bird_")) settings.loadType = AudioClipLoadType.DecompressOnLoad;
            else settings.loadType = AudioClipLoadType.CompressedInMemory;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = assetPath.Contains("/bird_");
        }
    }
}
