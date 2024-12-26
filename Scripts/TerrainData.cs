using Godot;
using System;

[GlobalClass]
public partial class TerrainData : Node
{
	[Export] public bool useTexture { get; private set; } = true;
	
	[Export] private Texture2D terrainIDTexture;
	public Image IdImage { get; private set; }
	
	public enum TerrainType { Asphalt, Dirt, Ice }
	[Export] public TerrainType terrainType;

    public override void _Ready()
    {
		if (terrainIDTexture.IsValid())
		{
        	IdImage = terrainIDTexture.GetImage();
			
			if (IdImage.IsCompressed())
			{
				var error = IdImage.Decompress();
				
				if (error != Error.Ok)
				{
					GD.PushWarning($"Something went wrong when decompressing the image in {Name} (Error: {error}).");
				}
			}
		}
    }
}
