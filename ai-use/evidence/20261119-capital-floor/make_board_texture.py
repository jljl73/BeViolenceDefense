from pathlib import Path
import numpy as np
from PIL import Image
source = Path('ProjectBeViolenceDefense/Assets/05_Art/06_Textures/Stage/Ground_Cobble.png')
output = source.with_name('Ground_Cobble_Board.png')
image = Image.open(source).convert('RGBA')
pixels = np.asarray(image).copy()
srgb = pixels[:, :, :3].astype(np.float64) / 255
linear = np.where(srgb <= .04045, srgb / 12.92, ((srgb + .055) / 1.055) ** 2.4)
mean = linear.mean(axis=(0, 1), keepdims=True)
# Preserve linear mean color; retain 35% of the original variations.
muted = mean + (linear - mean) * .35
result = np.where(muted <= .0031308, muted * 12.92, 1.055 * muted ** (1 / 2.4) - .055)
pixels[:, :, :3] = np.clip(np.rint(result * 255), 0, 255).astype(np.uint8)
Image.fromarray(pixels).save(output)
print(f'{output}: {image.size}, linear contrast multiplier 0.35')
