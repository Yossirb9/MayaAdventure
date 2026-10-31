"""Copy chosen glTF models (with their .bin buffers and textures) from the Quaternius packs into Assets/Resources.

python copy_models.py <dl dir> <project Assets dir>
"""
import json
import os
import shutil
import sys

dl, assets = sys.argv[1], sys.argv[2]
NATURE = os.path.join(dl, 'stylized-nature-megakit', 'Stylized Nature MegaKit[Standard]_x', 'glTF')
PLAT = os.path.join(dl, 'ultimate-platformer-pack', 'Ultimate Platformer Pack by Quaternius_x', 'Ultimate Platformer Pack - Dec 2021')

nature = ['CommonTree_1', 'CommonTree_2', 'CommonTree_3', 'CommonTree_4', 'CommonTree_5',
          'Pine_1', 'Pine_2', 'Pine_3', 'Pine_4', 'Pine_5', 'TwistedTree_1', 'TwistedTree_2', 'TwistedTree_3',
          'DeadTree_1', 'DeadTree_2', 'Bush_Common', 'Bush_Common_Flowers', 'Fern_1',
          'Flower_3_Group', 'Flower_4_Group', 'Flower_3_Single', 'Flower_4_Single',
          'Grass_Common_Short', 'Grass_Common_Tall', 'Grass_Wispy_Short', 'Grass_Wispy_Tall', 'Clover_1', 'Clover_2',
          'Mushroom_Common', 'Mushroom_Laetiporus', 'Plant_1', 'Plant_7',
          'Rock_Medium_1', 'Rock_Medium_2', 'Rock_Medium_3', 'Pebble_Round_1', 'Pebble_Round_2', 'Pebble_Round_3',
          'RockPath_Round_Wide', 'RockPath_Round_Small_1', 'RockPath_Round_Small_2']
platformer = {
    'Enemies': ['Enemy', 'Bee', 'Crab', 'Skull'],
    'Powerups and Pickups': ['Coin', 'Star', 'Heart', 'Gem_Blue', 'Gem_Green', 'Gem_Pink', 'Key', 'Thunder'],
    'Level and Mechanics': ['Cannon', 'Cannonball', 'Bomb', 'Bouncer', 'Chest', 'Goal_Flag', 'Hazard_Saw', 'Spikes',
                            'SpikyBall', 'Bridge_Small', 'Bridge_Modular', 'Bridge_Modular_Center', 'Fence_1',
                            'Fence_Middle', 'Fence_Corner', 'Pipe_End', 'Pipe_Straight', 'Plant_Large', 'Plant_Small',
                            'Lever', 'Arrow', 'Tower', 'Door', 'Hazard_SpikeTrap'],
    'Nature': ['Cloud_1', 'Cloud_2', 'Cloud_3', 'RockPlatforms_1', 'RockPlatforms_2', 'RockPlatforms_3',
               'RockPlatforms_Large', 'RockPlatforms_Medium', 'RockPlatform_Tall', 'Rock_1', 'Rock_2', 'Bush', 'Grass_1'],
    'Cubes': ['Cube_Bricks', 'Cube_Question', 'Cube_Crate', 'Cube_Exclamation', 'Cube_Grass_Single', 'Cube_Spikes'],
}


def copy_gltf(src_dir, name, dst_dir):
    os.makedirs(dst_dir, exist_ok=True)
    src = os.path.join(src_dir, name + '.gltf')
    if not os.path.exists(src):
        print('missing', src)
        return
    j = json.load(open(src))
    shutil.copy2(src, dst_dir)
    for item in j.get('buffers', []) + j.get('images', []):
        uri = item.get('uri', '')
        if uri and not uri.startswith('data:'):
            shutil.copy2(os.path.join(src_dir, uri), os.path.join(dst_dir, uri))


for n in nature:
    copy_gltf(NATURE, n, os.path.join(assets, 'Resources', 'Nature'))
for folder, names in platformer.items():
    for n in names:
        copy_gltf(os.path.join(PLAT, folder, 'glTF'), n, os.path.join(assets, 'Resources', 'Platformer'))
for lic_src, lic_dst in ((os.path.join(PLAT, 'License.txt'), 'Platformer'),):
    shutil.copy2(lic_src, os.path.join(assets, 'Resources', lic_dst, 'License-Quaternius-CC0.txt'))
shutil.copy2(os.path.join(PLAT, 'License.txt'), os.path.join(assets, 'Resources', 'Nature', 'License-Quaternius-CC0.txt'))
print('done')
