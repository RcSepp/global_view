/Users/sklaassen/Desktop/ffmpeg/ffmpeg -i frames/frame%05d.png -c:v libx264 -vf fps=20 -pix_fmt yuv420p movie.mp4