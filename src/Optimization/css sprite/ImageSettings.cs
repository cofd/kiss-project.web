﻿using System.Drawing;

namespace Kiss.Web.Optimization
{
    /// <summary>
    /// ImageSettings is a class used to store the settings retrieved from the sprite settings file
    /// </summary>
    class ImageSettings
    {
        /// <summary>
        /// The output image file format
        /// </summary>
        public string Format { get; set; }
        /// <summary>
        /// The quality level of the format, if the format supports quality settings (such as jpg)
        /// </summary>
        public int Quality { get; set; }
        /// <summary>
        /// The maximum size of a sprite before it will be split into multiple images
        /// </summary>
        public int MaxSize { get; set; }
        /// <summary>
        /// The background color of the output sprite
        /// </summary>
        public Color BackgroundColor { get; set; }

        /// <summary>
        /// Controls whether the application will tile images along the X or Y axis
        /// </summary>
        public bool TileInYAxis { get; set; }

        // Constructor inputs default values
        public ImageSettings()
        {
            Format = "png";
            Quality = 90;
            MaxSize = 500;
            BackgroundColor = Color.Transparent;
        }
    }
}
