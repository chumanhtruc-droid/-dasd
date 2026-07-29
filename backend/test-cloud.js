const cloudinary = require('cloudinary').v2;

cloudinary.config({
  cloud_name: "qncudzpu",
  api_key: "498533888516325",
  api_secret: "tnjs2lbGrew86ayDYwK9bmNrpjl"
});

cloudinary.uploader.upload("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=", { public_id: "test" }, function(error, result) {
  if (error) {
    console.error("ERROR:", error);
  } else {
    console.log("SUCCESS:", result.secure_url);
  }
});
