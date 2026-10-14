import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class BackgroundImageService {
  private readonly genericBackgrounds = [
    '/generic_background1.webp',
    '/generic_background2.webp',
    '/generic_background3.webp',
    '/generic_background4.jpg',
    '/generic_background5.jpg'
  ];

  public getAuthBackgroundImage(): string {
    const randomIndex = Math.floor(Math.random() * this.genericBackgrounds.length);
    const selectedImage = this.genericBackgrounds[randomIndex];

    return `linear-gradient(115deg, rgba(15,23,42,0.72), rgba(2,6,23,0.58)), url('${selectedImage}')`;
  }
}