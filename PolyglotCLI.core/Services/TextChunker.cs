using System;
using System.Collections.Generic;
using System.Text;

namespace PolyglotCLI
{
    public static class TextChunker
    {
        public static List<string> ChunkText(string text, int maxCharacters = 6000, int overlapCharacters = 0)
        {
            var chunks = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return chunks;
            }

            // Clamp overlap to a reasonable max (half of chunk size)
            if (overlapCharacters < 0) overlapCharacters = 0;
            if (overlapCharacters > maxCharacters / 2) overlapCharacters = maxCharacters / 2;

            // Split by lines
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var currentChunk = new StringBuilder();

            foreach (var line in lines)
            {
                if (currentChunk.Length + line.Length + 1 > maxCharacters)
                {
                    if (currentChunk.Length > 0)
                    {
                        string chunkContent = currentChunk.ToString();
                        chunks.Add(chunkContent);

                        // Apply overlap: carry the last N characters into the next chunk
                        currentChunk.Clear();
                        if (overlapCharacters > 0 && chunkContent.Length > 0)
                        {
                            int overlapStart = Math.Max(0, chunkContent.Length - overlapCharacters);
                            string overlap = chunkContent.Substring(overlapStart);
                            currentChunk.Append(overlap);
                        }
                    }
                    
                    // If a single line is longer than maxCharacters, chunk it by characters
                    if (line.Length > maxCharacters)
                    {
                        int index = 0;
                        while (index < line.Length)
                        {
                            int length = Math.Min(maxCharacters, line.Length - index);
                            chunks.Add(line.Substring(index, length));
                            index += length;
                        }
                        continue;
                    }
                }

                if (currentChunk.Length > 0)
                {
                    currentChunk.AppendLine();
                }
                currentChunk.Append(line);
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString());
            }

            return chunks;
        }
    }
}
