﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Text;

namespace WFImageParser
{
    public class RivenPreparer
    {
        public void PrepareRiven(string imagePath, string outputPath)
        {
            var converter = new ColorSpaceConverter();
            using (Image<Rgba32> image = Image.Load(imagePath))
            {
                using (Image<Rgba32> outputImage = new Image<Rgba32>(500, 765))
                {
                    //Copy title/modis
                    for (int x = 1800; x < 2300; x++)
                    {
                        for (int y = 525; y < 1155; y++)
                        {
                            if(image[x,y].R == 172 && image[x,y].G == 131 && image[x,y].B == 213)
                                outputImage[x - 1800, y - 525] = Rgba32.Black;
                            else
                                outputImage[x - 1800, y - 525] = Rgba32.White;
                        }
                    }
                    //Copy Drain
                    for (int x = 2225; x < 2285; x++)
                    {
                        for (int y = 465; y < 510; y++)
                        {
                            if (image[x, y].R == 172 && image[x, y].G == 131 && image[x, y].B == 213)
                                outputImage[x - 2225, y - 510 + 630 + 45] = Rgba32.Black;
                            else
                                outputImage[x - 2225, y - 510 + 630 + 45] = Rgba32.White;
                        }
                    }
                    //Copy MR
                    for (int x = 1885; x < 1975; x++)
                    {
                        for (int y = 1165; y < 1210; y++)
                        {
                            if (image[x, y].R == 172 && image[x, y].G == 131 && image[x, y].B == 213)
                                outputImage[x - 1885, y - 1165 + 630 + 45] = Rgba32.Black;
                            else
                                outputImage[x - 1885, y - 1165 + 630 + 45] = Rgba32.White;
                        }
                    }
                    //Copy rerolls
                    for (int x = 2212; x < 2270; x++)
                    {
                        for (int y = 1166; y < 1211; y++)
                        {
                            if (image[x, y].R == 172 && image[x, y].G == 131 && image[x, y].B == 213)
                                outputImage[x - 2212, y - 1166 + 630 + 45 + 45] = Rgba32.Black;
                            else
                                outputImage[x - 2212, y - 1166 + 630 + 45 + 45] = Rgba32.White;
                        }
                    }
                    outputImage.Save(outputPath);
                }
            }
        }
    }
}