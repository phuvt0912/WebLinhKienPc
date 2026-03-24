// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
    var swiper = new Swiper(".mySwiper", {
        slidesPerView: "auto",
    spaceBetween: 20,
    });

    document.addEventListener('DOMContentLoaded', function () {

    const track = document.querySelector('.slider-track');
    const next = document.querySelector('.next');
    const prev = document.querySelector('.prev');

    if (!track) return;

    let index = 0;

    function getWidth() {
        return track.children[0].offsetWidth + 16;
    }

    function update() {
        track.style.transform = `translateX(${-index * getWidth()}px)`
    }

    next.addEventListener('click', () => {
        if (index < track.children.length - 1) {
        index++;
    update();
        }
    });

    prev.addEventListener('click', () => {
        if (index > 0) {
        index--;
    update();
        }
    });

    });
var bannerSwiper = new Swiper(".bannerSwiper", {
    loop: true,
    autoplay: {
        delay: 3000,
    },
    pagination: {
        el: ".swiper-pagination",
        clickable: true,
    },
});

